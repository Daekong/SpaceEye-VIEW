using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Interfaces;
using SpaceEye.Core.Celestial;
using SpaceEye.Core.Common;
using SpaceEye.Scene.Interfaces;
using SpaceEye.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;
using System.IO;

namespace SpaceEye.Scene.Celestial
{
    /// <summary>
    /// 3D 맵 엔진의 핵심이 되는 테셀레이션 기반 지구(Earth) 렌더링 노드입니다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>기존의 무거운 구체(Sphere) 정점 배열 대신, 단 8개의 정점을 가진 정육면체(Cube)를 GPU로 전송합니다.</description></item>
    /// <item><description>GPU의 하드웨어 테셀레이터(TCS, TES)를 활용하여 실시간으로 정육면체를 수만 개의 폴리곤으로 분할하고 완벽한 구형으로 변형합니다.</description></item>
    /// <item><description>위성 데이터와 같은 거대한 스케일의 좌표계 오류를 방지하기 위해 셰이더 내부까지 64비트(<c>double</c>) 정밀도를 유지합니다.</description></item>
    /// </list>
    /// </remarks>
    internal class EarthNodeTES : ISceneNode, ITimeUpdateable, IDisposable
    {
        #region # Feild

        /// <summary>
        /// GPU에 저장된 정점 속성 상태를 보관하는 Vertex Array Object (VAO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vao;

        /// <summary>
        /// 실제 정점 데이터(위치 좌표)를 GPU 메모리에 보관하는 Vertex Buffer Object (VBO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vbo;

        /// <summary>
        /// 정점들의 연결 순서를 정의하는 인덱스 데이터를 GPU 메모리에 보관하는 Element Buffer Object (EBO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _ebo;

        /// <summary>
        /// 정점(VS), 제어(TCS), 평가(TES), 단편(FS) 셰이더가 링크된 최종 셰이더 프로그램의 OpenGL 식별자입니다.
        /// </summary>
        private int _shader;

        /// <summary>
        /// 렌더링할 총 인덱스의 개수입니다. 
        /// 정육면체의 6개 면이 각각 사각형(4개의 정점)을 이루어 총 24개(6 * 4)가 됩니다.
        /// </summary>
        private int _indexCount = 24;

        /// <summary>현재 지구의 자전 각도 (Degrees)입니다.</summary>
        private double _rotationAngleDeg = 0;

        // 지구 텍스처 ID 변수
        private int _texture; 

        #region # 정육면체(Cube) 정점 및 인덱스 데이터

        /// <summary>
        /// 테셀레이션의 뼈대가 되는, 중심이 원점이고 각 변의 길이가 2(-1.0 ~ 1.0)인 단위 정육면체의 고유 정점 8개 배열입니다.
        /// 각 정점은 배정밀도 부동소수점(<c>double</c>) 형태의 3D 좌표로 구성되어 있습니다.
        /// </summary>
        private readonly double[] _vertices = {
            -1.0, -1.0,  1.0, // 0: 앞-왼-아래
             1.0, -1.0,  1.0, // 1: 앞-오-아래
             1.0,  1.0,  1.0, // 2: 앞-오-위
            -1.0,  1.0,  1.0, // 3: 앞-왼-위
            -1.0, -1.0, -1.0, // 4: 뒤-왼-아래
             1.0, -1.0, -1.0, // 5: 뒤-오-아래
             1.0,  1.0, -1.0, // 6: 뒤-오-위
            -1.0,  1.0, -1.0  // 7: 뒤-왼-위
        };

        /// <summary>
        /// 8개의 고유 정점을 조합하여 정육면체의 6개 면(Face)을 구성하는 인덱스 배열입니다.
        /// 테셀레이션 셰이더의 사각형(Quad) 처리를 위해 각 면은 4개의 정점으로 묶여 있으며, 표면 방향 계산을 위해 반시계 방향(CCW)으로 정렬되어 있습니다.
        /// </summary>
        private readonly uint[] _indices = {
            0, 1, 2, 3, // 앞면 (+Z)
            1, 5, 6, 2, // 우측면 (+X)
            5, 4, 7, 6, // 뒷면 (-Z)
            4, 0, 3, 7, // 좌측면 (-X)
            3, 2, 6, 7, // 윗면 (+Y)
            4, 5, 1, 0  // 아랫면 (-Y)
        };

        #endregion

        #endregion        

        #region # Constructor & Initialize

        /// <summary>
        /// <see cref="EarthNode"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <remarks>접근 제한자가 internal로 설정되어 엔진 외부에서의 직접 생성을 차단합니다.</remarks>
        public EarthNodeTES()
        {
        }

        /// <summary>
        /// 셰이더를 컴파일하고 GPU에 정점 데이터를 할당하여 렌더링을 준비합니다.
        /// </summary>
        public void Initialize()
        {
            string vertexShaderSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "VertexShaders", "Earth.vert") ;
            string tcsSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "TCSShaders", "Earth.tcs");
            string tesSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "TESShaders", "Earth.tes");
            string fragmentShaderSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "FragmentShaders", "Earth.frag"); ;


            _shader = ShaderCompiler.CreateProgramFromFiles(
                vertexShaderSource, tcsSource, tesSource, fragmentShaderSource);

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            // 예: 8K 해상도의 일반적인 평면 세계지도 이미지 파일
            // 1. Resource DLL의 이름 (예: SpaceEye.Resources)
            string basePath = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Resouces", "Images", "BaseMap");

            _texture = TextureLoader.LoadTexture(Path.Combine(basePath, "Earth_BlueMarble_NextGeneration_2Km.jpg"));

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(double), _vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribLPointer(0, 3, VertexAttribDoubleType.Double, 3 * sizeof(double), IntPtr.Zero);

            GL.BindVertexArray(0);            
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
            _rotationAngleDeg += Earth.EarthRotationSpeedDegPerSec * deltaSeconds * timeScale;

            // 360도 도달 시 다시 0도로 순환 (부동 소수점 정밀도 유지)
            _rotationAngleDeg %= 360.0;
        }

        #endregion

        #region # Public Method

        /// <summary>
        /// 화면에 지구 노드를 그립니다. 매 프레임마다 호출됩니다.
        /// </summary>
        /// <param name="view">카메라의 위치와 방향을 나타내는 뷰 행렬 (64비트)</param>
        /// <param name="projection">카메라의 원근감을 나타내는 투영 행렬 (64비트)</param>
        public void Draw(Matrix4d view, Matrix4d projection)
        {
            Matrix4d model = Matrix4d.CreateRotationY(_rotationAngleDeg.ToRadian());

            Matrix4d invView = view.Inverted();
            Vector3d camPos = new Vector3d(invView.Row3.X, invView.Row3.Y, invView.Row3.Z);

            GL.UseProgram(_shader);

            // 3. 모델, 뷰, 투영 행렬 전송
            int modelLoc = GL.GetUniformLocation(_shader, "model");
            int viewLoc = GL.GetUniformLocation(_shader, "view");
            int projLoc = GL.GetUniformLocation(_shader, "projection");
            int radiusLoc = GL.GetUniformLocation(_shader, "radius");
            int texLoc = GL.GetUniformLocation(_shader, "earthTexture");
            int camPosLoc = GL.GetUniformLocation(_shader, "cameraPos");

            if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref model); 
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);            
            if (radiusLoc != -1) GL.Uniform1(radiusLoc, Earth.EarthRadius);
            if (camPosLoc != -1) GL.Uniform3(camPosLoc, camPos.X, camPos.Y, camPos.Z);

            if (texLoc != -1) GL.Uniform1(texLoc, 0);            
            
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);

            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.BindVertexArray(_vao);
            GL.PatchParameter(PatchParameterInt.PatchVertices, 4);
            GL.DrawElements(PrimitiveType.Patches, _indexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);            

            GL.BindVertexArray(0);
            GL.UseProgram(0);

            ErrorCode code = GL.GetError();
        }

        #endregion

        #region # IDisposable

        /// <summary>
        /// 할당된 모든 OpenGL 자원(VBO, VAO, Shader 등)을 메모리에서 해제합니다.
        /// </summary>
        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteProgram(_shader);
        }

        #endregion
    }
}
