using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Extensions;
using SpaceEye.Common.Interfaces;
using SpaceEye.Core;
using SpaceEye.Core.Celestial;
using SpaceEye.Scene.Interfaces;

namespace SpaceEye.Scene
{
    /// <summary>
    /// 지구 행성을 렌더링하는 내부 전용 노드 클래스입니다.
    /// </summary>
    /// <remarks> 
    /// 이 클래스는 Km 단위의 거대 스케일 우주 환경에서 발생하는 정밀도 손실(Jittering) 현상을 방지하기 위해 
    /// 정점 데이터 생성부터 셰이더 연산까지 모든 과정을 <see cref="Double"/> 정밀도로 처리합니다.  
    /// </remarks>
    internal class EarthNode : ISceneNode, ITimeUpdateable, IDisposable
    {
        #region # Fields

        /// <summary>정점 배열 객체(VAO) ID입니다.</summary>
        private int _vao;
        /// <summary>정점 버퍼 객체(VBO) ID입니다.</summary>
        private int _vbo;
        /// <summary>인덱스 버퍼 객체(EBO) ID입니다.</summary>
        private int _ebo;
        /// <summary>컴파일된 64비트 전용 셰이더 프로그램 ID입니다.</summary>
        private int _shader;
        /// <summary>렌더링할 삼각형 인덱스의 총 개수입니다.</summary>
        private int _indexCount;
        /// <summary>리소스 초기화 완료 여부를 나타내는 플래그입니다.</summary>
        private bool _isInitialized = false;
        /// <summary>현재 지구의 자전 각도 (Degrees)입니다.</summary>
        private double _rotationAngleDeg = 0;
        #endregion

        #region # Constructor & Initialize

        /// <summary>
        /// <see cref="EarthNode"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <remarks>접근 제한자가 internal로 설정되어 엔진 외부에서의 직접 생성을 차단합니다.</remarks>
        public EarthNode()
        {
        }

        /// <summary>
        /// 지구 렌더링에 필요한 GPU 자원을 할당하고 64비트 정점 데이터를 비디오 메모리에 업로드합니다.
        /// </summary>
        /// <remarks>
        /// <see cref="SphereGenerator"/>를 통해 생성된 <c>double[]</c> 데이터를 
        /// <see cref="GL.VertexAttribLPointer"/>를 사용하여 GPU에 64비트 형식을 유지한 채 전달합니다.
        /// </remarks>
        public void Initialize()
        {
            if (_isInitialized) return;

            // 1. 64비트 구체 데이터 생성
            var (vertices, indices) = SphereGenerator.GenerateSphere(Earth.EarthRadius, 32);
            _indexCount = indices.Length;

            // 2. FP64 연산을 지원하는 고정밀 셰이더 프로그램 생성
            _shader = CreateDoublePrecisionShader();

            // 3. OpenGL 객체 생성
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            // 4. VBO 설정: 정점 위치(XYZ) 및 텍스처 좌표(UV)를 포함한 double 배열 업로드
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(double), vertices, BufferUsageHint.StaticDraw);

            // 5. EBO 설정: 삼각형 구성을 위한 인덱스 데이터 업로드
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // 6. 정점 속성 정의 (Layout 0: Position)
            // 핵심: VertexAttribLPointer 메서드는 Double 데이터를 32비트로 변환하지 않고 64비트 그대로 셰이더에 전달합니다.
            // Stride: 정점 하나당 (X, Y, Z, U, V) 5개의 double을 가지므로 5 * 8 bytes입니다.
            GL.VertexAttribLPointer(0, 3, VertexAttribDoubleType.Double, 5 * sizeof(double), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);

            GL.BindVertexArray(0);
            _isInitialized = true;
        }

        #endregion

        #region # ITimeUpdateable

        /// <summary>
        /// 매 프레임마다 지구의 자전 각도를 갱신합니다.
        /// </summary>
        public void Update(double deltaSeconds)
        {
            // 시뮬레이션 속도 배율 (예: 1.0은 실시간, 3600.0은 1시간을 1초에 진행)
            double timeScale = 1000.0;

            // Degree 기반 각도 계산
            _rotationAngleDeg -= Earth.EarthRotationSpeedDegPerSec * deltaSeconds * timeScale;

            // 360도 도달 시 다시 0도로 순환 (부동 소수점 정밀도 유지)
            _rotationAngleDeg %= 360.0;
        }

        #endregion

        #region # Public Method

        /// <summary>
        /// 주입받은 64비트 시점 행렬을 사용하여 지구를 화면에 렌더링합니다.
        /// </summary>
        /// <param name="view">카메라의 64비트 뷰 행렬입니다.</param>
        /// <param name="projection">카메라의 64비트 투영 행렬입니다.</param>
        /// <remarks>
        /// 모든 행렬 연산은 GPU 내부에서 <c>dmat4</c>(64비트)를 통해 수행되어 우주적 스케일의 정밀도를 유지합니다.
        /// </remarks>
        public void Draw(Matrix4d view, Matrix4d projection)
        {
            if (!_isInitialized) return;

            // 1. Degree를 Radian으로 변환하여 회전 행렬 생성
            // OpenTK의 MathHelper.DegreesToRadians를 사용하거나 (angle * PI / 180.0)을 수행합니다.           
            Matrix4d model = Matrix4d.CreateRotationY(_rotationAngleDeg.ToRadian());

            GL.UseProgram(_shader);
            GL.Enable(EnableCap.DepthTest); // 깊이 테스트 활성화 (지구의 앞/뒷면 구분)           

            // 유니폼 위치 검색 및 Matrix4d 데이터 전송
            int mLoc = GL.GetUniformLocation(_shader, "model");
            int vLoc = GL.GetUniformLocation(_shader, "view");
            int pLoc = GL.GetUniformLocation(_shader, "projection");

            // OpenTK 3.3.3은 ref Matrix4d 오버로딩을 통해 유니폼 전송을 지원합니다.
            GL.UniformMatrix4(mLoc, false, ref model);
            GL.UniformMatrix4(vLoc, false, ref view);
            GL.UniformMatrix4(pLoc, false, ref projection);

            // VAO 바인딩 후 인덱스 기반 삼각형 그리기 수행
            GL.BindVertexArray(_vao);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);

            // 상태 복구
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        /// <summary>
        /// 64비트 고정밀 연산을 수행하는 셰이더 프로그램을 생성하고 컴파일합니다.
        /// </summary>
        /// <returns>생성된 셰이더 프로그램의 정수 ID입니다.</returns>
        private int CreateDoublePrecisionShader()
        {
            // 중요: #version 앞에 공백이 단 한 칸도 있으면 안 됩니다.
            string vSrc = "#version 410 core\n" +
                          "layout(location = 0) in dvec3 aPos;\n" +
                          "uniform dmat4 model;\n" +
                          "uniform dmat4 view;\n" +
                          "uniform dmat4 projection;\n" +
                          "void main() {\n" +
                          "    dvec4 clipPos = projection * view * model * dvec4(aPos, 1.0);\n" +
                          "    gl_Position = vec4(clipPos);\n" +
                          "}\n";

            string fSrc = "#version 410 core\n" +
                          "out vec4 FragColor;\n" +
                          "void main() {\n" +
                          "    FragColor = vec4(0.2, 0.5, 0.8, 1.0);\n" +
                          "}\n";

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vSrc);
            GL.CompileShader(vs);
            CheckShaderCompileStatus(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fSrc);
            GL.CompileShader(fs);
            CheckShaderCompileStatus(fs);

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);

            // 링크 후 개별 셰이더 객체는 삭제하여 메모리 관리
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            return prog;
        }

        /// <summary>
        /// 셰이더의 컴파일 상태를 확인하고 실패 시 로그를 출력합니다.
        /// </summary>
        /// <param name="shader">검사할 셰이더 객체 ID입니다.</param>
        private void CheckShaderCompileStatus(int shader)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                System.Diagnostics.Debug.WriteLine($"[Shader Error]: {info}");
            }
        }

        #endregion

        #region # IDisposable

        /// <summary>
        /// 할당된 모든 OpenGL 자원(VBO, VAO, Shader 등)을 메모리에서 해제합니다.
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);
                GL.DeleteVertexArray(_vao);
                GL.DeleteProgram(_shader);
                _isInitialized = false;
            }
        }

        #endregion
    }
}
