using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Core.Common;
using SpaceEye.Core.Orbit;
using SpaceEye.Scene.Celestial;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Scene.Orbit
{
    /// <summary>
    /// OrbitManager에 종속되어 GPU 인스턴싱 렌더링(Instancing)을 직접 수행하는 씬 노드(Scene Node)입니다.
    /// </summary>
    /// <remarks>
    /// 이 클래스는 <see cref="ISceneNode"/>를 구현하여 UniverseScene에 등록됩니다.
    /// 자체적인 데이터 연산은 하지 않으며, 오직 <see cref="OrbitManager"/>가 준비한 버퍼를 읽어 화면에 그리는 역할만 담당합니다.
    /// </remarks>
    internal class OrbitSystemNode : ISceneNode, IDisposable
    {
        #region # Private Feilds

        /// <summary>
        /// 렌더링할 데이터 원본을 소유하고 있는 부모 매니저 객체입니다.
        /// </summary>
        private readonly OrbitManager _manager;

        /// <summary>
        /// 궤도 좌표를 계산(Baking)하기 위한 Compute Shader 프로그램 식별자입니다.
        /// </summary>
        private int _computeShaderProgram;

        /// <summary>
        /// 계산된 좌표를 화면에 그리기 위한 렌더링 셰이더 프로그램 식별자입니다. 
        /// </summary>
        private int _renderShaderProgram;

        /// <summary>
        /// 더미(Dummy) 정점 배열 객체(VAO)의 식별자입니다.
        /// </summary>
        /// <remarks>
        /// Zero-Copy 렌더링에서는 정점 데이터를 VBO가 아닌 SSBO에서 직접 읽어오므로, 
        /// 실제 정점 버퍼는 없지만 OpenGL 파이프라인을 작동시키기 위한 빈 VAO가 하나 필요합니다.
        /// </remarks>
        private int _vao;

        /// <summary>
        /// TLE 데이터를 저장하는 GPU 측 SSBO의 식별자입니다. (Binding = 0)
        /// </summary>
        private int _tleDataSsbo;

        /// <summary>
        /// Compute Shader가 SGP4 연산 후 도출한 3D 좌표(X, Y, Z)를 저장할 GPU 측 SSBO의 식별자입니다. (Binding = 1)
        /// </summary>
        private int _positionSsbo;

        /// <summary>
        /// GPU 메모리에 한 번에 할당할 최대 위성 개수입니다.
        /// </summary>
        /// <remarks>메모리 재할당으로 인한 프레임 드랍을 막기 위해 초기에 100,000개 정도의 넉넉한 공간을 미리 확보합니다.</remarks>
        private readonly int _maxSatelliteCount;

        /// <summary>
        /// <see cref="GpuTleData"/> 구조체의 1개당 바이트 크기입니다. (Pack=8 정렬 기준 56 bytes)
        /// </summary>
        private readonly int _tleDataSize;

        #endregion

        #region # Properties

        #endregion

        #region # Constructor

        /// <summary>
        /// <see cref="OrbitSystemNode"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="manager">이 노드를 제어할 <see cref="OrbitManager"/> 인스턴스입니다.</param>
        /// <param name="maxSatelliteCount">미리 할당할 최대 위성의 개수입니다. 기본값은 100,000개입니다.</param>
        public OrbitSystemNode(OrbitManager manager, int maxSatelliteCount = 10000)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _maxSatelliteCount = maxSatelliteCount;

            // 구조체의 크기를 미리 계산하여 캐싱 (마샬링 성능 최적화)
            _tleDataSize = Marshal.SizeOf<OrbitDataForGpu>();         
        }

        #endregion

        #region # Shader Creation Methods

        /// <summary>
        /// 외부 파일로부터 셰이더 소스를 읽어와 Compute Shader 프로그램과 Render Shader 프로그램을 각각 생성합니다.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Compute Shader는 SGP4/Keplerian 연산을 위해 별도의 단일 스테이지 프로그램으로 생성됩니다.</description></item>
        /// <item><description>Render Shader는 Vertex/Fragment 단계를 포함하며, 기존 <see cref="ShaderCompiler"/>를 활용하여 생성됩니다.</description></item>
        /// <item><description>초기화 단계(<see cref="Initialize"/>)에서 이 메서드를 호출하여 <c>_computeShaderProgram</c>과 <c>_renderShaderProgram</c> 필드를 설정해야 합니다.</description></item>
        /// </list>
        /// </remarks>
        private void CreateShaders()
        {
            // 1. 파일 경로 설정 (기존 프로젝트 구조에 맞춰 수정 가능)            
            string shaderBasePath = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Orbit");
            string computePath = Path.Combine(shaderBasePath, "Keplerian.glsl");
            string vertexPath = Path.Combine(shaderBasePath, "Keplerian.vert");
            string fragmentPath = Path.Combine(shaderBasePath, "Keplerian.frag");      

            // 2. Compute Shader 프로그램 생성
            if (File.Exists(computePath))
            {
                string computeSource = File.ReadAllText(computePath);
                _computeShaderProgram = CreateComputeProgram(computeSource);
            }
            else
            {
                throw new FileNotFoundException("Compute Shader 파일을 찾을 수 없습니다.", computePath);
            }

            // 3. Render Shader 프로그램 생성 (기존 ShaderCompiler 활용)
            if (File.Exists(vertexPath) && File.Exists(fragmentPath))
            {
                _renderShaderProgram = ShaderCompiler.CreateProgram(vertexPath, fragmentPath);
            }
            else
            {
                throw new FileNotFoundException("Render Shader(Vert/Frag) 파일을 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 단일 Compute Shader 소스 코드를 컴파일하고 링크하여 GPU 프로그램을 생성합니다.
        /// </summary>
        /// <param name="source">GLSL Compute Shader 소스 코드 문자열입니다.</param>
        /// <returns>생성된 OpenGL 셰이더 프로그램의 식별자(ID)를 반환합니다.</returns>
        /// <exception cref="Exception">셰이더 컴파일 실패 또는 링크 오류 시 예외를 발생시킵니다.</exception>
        private int CreateComputeProgram(string source)
        {
            // 1. 셰이더 객체 생성 및 컴파일
            int shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            // 컴파일 상태 확인
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                GL.DeleteShader(shader);
                throw new Exception($"Compute Shader 컴파일 실패:\n{infoLog}");
            }

            // 2. 프로그램 생성 및 셰이더 부착
            int program = GL.CreateProgram();
            GL.AttachShader(program, shader);
            GL.LinkProgram(program);

            // 링크 상태 확인
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkSuccess);
            if (linkSuccess == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                GL.DeleteProgram(program);
                GL.DeleteShader(shader);
                throw new Exception($"Compute Shader 프로그램 링크 실패:\n{infoLog}");
            }

            // 3. 사용 완료된 셰이더 객체 분리 및 삭제 (메모리 관리)
            GL.DetachShader(program, shader);
            GL.DeleteShader(shader);

            return program;
        }

        #endregion

        #region # ISceneNode

        /// <summary>
        /// 셰이더를 컴파일하고 GPU에 정점 데이터를 할당하여 렌더링을 준비합니다.
        /// </summary>
        public void Initialize()
        {
            CreateShaders();

            // 1. 더미 VAO 생성 및 바인딩
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            // 2. TLE 데이터용 SSBO 생성 및 메모리 사전 할당 (Binding = 0)
            _tleDataSsbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _tleDataSsbo);

            // 크기: (최대 위성 수 * 구조체 크기), IntPtr.Zero를 넘겨서 빈 메모리 공간만 예약 (DynamicDraw)
            GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr)(_maxSatelliteCount * _tleDataSize), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            // Compute Shader 및 Vertex Shader의 'layout(std430, binding = 0)'에 연결
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _tleDataSsbo);

            // 3. 계산된 정점 좌표(Position)용 SSBO 생성 및 메모리 사전 할당 (Binding = 1)
            // dvec4(X, Y, Z, W)는 FP64 기준 8바이트 * 4 = 32바이트입니다.
            int positionDataSize = 32;
            _positionSsbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _positionSsbo);

            // Compute Shader가 쓰고, Vertex Shader가 읽으므로 용도를 DynamicCopy로 설정
            GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr)(_maxSatelliteCount * positionDataSize), IntPtr.Zero, BufferUsageHint.DynamicCopy);

            // 'layout(std430, binding = 1)'에 연결
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _positionSsbo);

            // 상태 안전 해제
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// 현재 카메라 시점에 맞춰 모든 위성 궤도를 화면에 렌더링합니다.
        /// </summary>
        /// <param name="view">카메라의 64비트 뷰 행렬(View Matrix)입니다.</param>
        /// <param name="projection">카메라의 64비트 투영 행렬(Projection Matrix)입니다.</param>
        public void Draw(Matrix4d view, Matrix4d projection)
        {
            // 1. 매니저에게 필요한 전역 데이터 직접 요청
            int activeCount = _manager.ActiveSatelliteCount;
            if (activeCount == 0) return;

            double currentTime = _manager.CurrentTimeSinceEpoch;

            // ==========================================
            // 단계 1: Compute Shader 실행 (데이터 굽기)
            // ==========================================
            GL.UseProgram(_computeShaderProgram);

            int timeLoc = GL.GetUniformLocation(_computeShaderProgram, "u_timeSinceEpoch");
            GL.Uniform1(timeLoc, currentTime);

            int workGroups = (activeCount + 255) / 256;
            GL.DispatchCompute(workGroups, 1, 1);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            // ==========================================
            // 단계 2: Vertex Shader 렌더링 (화면에 그리기)
            // ==========================================
            GL.UseProgram(_renderShaderProgram);

            // 카메라 행렬 전송
            int viewLoc = GL.GetUniformLocation(_renderShaderProgram, "view");
            int projLoc = GL.GetUniformLocation(_renderShaderProgram, "projection");

            // (Float로 캐스팅하여 넘기거나 UniformMatrix4x8 사용)
            // SendMatrixToShader(viewLoc, view);
            // SendMatrixToShader(projLoc, projection);

            // 💡 Visibility SSBO 바인딩 코드 삭제됨! 
            // 이미 이 Draw가 호출되기 직전에 SceneRenderer가 2번 슬롯에 꽂아두었기 때문에 
            // 여기서 다시 바인딩할 필요가 없습니다.

            GL.BindVertexArray(_vao);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 1, activeCount);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        #endregion

        #region # Public Method

        /// <summary>
        /// C# 메모리에 있는 최신 TLE 데이터 배열을 GPU의 SSBO로 초고속 전송합니다.
        /// </summary>
        /// <param name="tleArray">전송할 TLE 데이터 구조체의 원본 배열입니다.</param>
        /// <param name="activeCount">현재 활성화된 위성의 개수입니다.</param>
        /// <remarks>
        /// 데이터가 추가되거나 변경되었을 때 <see cref="OrbitManager"/>가 이 메서드를 호출(Dirty 플래그 패턴)합니다.
        /// 전체 버퍼를 재생성하지 않고 변경된 크기만큼만 <see cref="GL.BufferSubData"/>를 사용해 덮어씌웁니다.
        /// </remarks>
        public void SyncOrbitDataToGpu(OrbitDataForGpu[] orbitDataArray, int activeCount)
        {
            if (activeCount == 0 || orbitDataArray == null) return;

            // 허용된 최대 위성 수를 초과하는 경우 안전하게 잘라냄 (또는 크기 확장 로직 호출 가능)
            int uploadCount = Math.Min(activeCount, _maxSatelliteCount);
            IntPtr uploadSize = (IntPtr)(uploadCount * _tleDataSize);

            // TLE SSBO에 바인딩하여 데이터 전송 (Zero-Copy의 시작점)
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _tleDataSsbo);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, uploadSize, orbitDataArray);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        #endregion       

        #region # IDisposable

        /// <summary>
        /// 위성 렌더링을 위해 할당된 VBO, VAO, SSBO 등 모든 GPU 리소스를 즉시 해제합니다.
        /// </summary>
        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_tleDataSsbo);
            GL.DeleteBuffer(_positionSsbo);
        }

        #endregion

    }
}
