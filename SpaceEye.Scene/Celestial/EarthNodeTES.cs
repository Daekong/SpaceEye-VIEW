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

        #region # 테셀레이션 셰이더 소스 (64비트 정밀도)

        /// <summary>
        /// 정점 셰이더(Vertex Shader)의 GLSL 소스 코드입니다. 
        /// 테셀레이션 파이프라인에서는 좌표 변환 없이 입력된 정점 위치(<c>aPos</c>)를 그대로 제어 셰이더(TCS)로 전달하는 역할만 수행합니다.
        /// </summary>
        private const string VertexShaderSource =
            "#version 410 core\n" +
            "layout (location = 0) in dvec3 aPos;\n" +
            "out dvec3 vPos;\n" +
            "void main() {\n" +
            "    vPos = aPos;\n" +
            "}\n";

        /// <summary>
        /// 테셀레이션 제어 셰이더(Tessellation Control Shader, TCS)의 GLSL 소스 코드입니다.
        /// 정점 셰이더로부터 넘겨받은 4개의 정점(Quad 패치)을 얼마나 세밀하게 쪼갤지 결정하는 테셀레이션 레벨(LOD)을 설정합니다.
        /// </summary>
        private const string TcsSource =
            "#version 410 core\n" +
            "layout (vertices = 4) out;\n" +
            "in dvec3 vPos[];\n" +
            "out dvec3 tcsPos[];\n" +
            "void main() {\n" +
            "    tcsPos[gl_InvocationID] = vPos[gl_InvocationID];\n" +
            "    if (gl_InvocationID == 0) {\n" +
            "        gl_TessLevelOuter[0] = 16.0;\n" +
            "        gl_TessLevelOuter[1] = 16.0;\n" +
            "        gl_TessLevelOuter[2] = 16.0;\n" +
            "        gl_TessLevelOuter[3] = 16.0;\n" +
            "        gl_TessLevelInner[0] = 16.0;\n" +
            "        gl_TessLevelInner[1] = 16.0;\n" +
            "    }\n" +
            "}\n";

        /// <summary>
        /// 테셀레이션 평가 셰이더(TES):
        /// 모델(Model) 행렬을 추가하여 회전 적용합니다.
        /// 텍스처 매핑을 위해 구형의 법선(Normal) 벡터를 단편 셰이더로 전달합니다.
        /// </summary>
        private const string TesSource =
            "#version 410 core\n" +
            "layout (quads, equal_spacing, ccw) in;\n" +
            "in dvec3 tcsPos[];\n" +
            "out vec3 vNormal;\n" +
            "uniform dmat4 model;\n" +
            "uniform dmat4 view;\n" +
            "uniform dmat4 projection;\n" +
            "uniform double radius;\n" +
            "void main() {\n" +
            "    double u = gl_TessCoord.x;\n" +
            "    double v = gl_TessCoord.y;\n" +
            "    dvec3 p0 = mix(tcsPos[0], tcsPos[1], u);\n" +
            "    dvec3 p1 = mix(tcsPos[3], tcsPos[2], u);\n" +
            "    dvec3 p = mix(p0, p1, v);\n" +
            "    \n" +
            "    dvec3 normal = normalize(p);\n" +
            "    vNormal = vec3(normal);\n" +           
            "    dvec3 spherePos = normal * radius;\n" +
            "    gl_Position = vec4(projection * view * model * dvec4(spherePos, 1.0lf));\n" +
            "}\n";

        /// <summary>
        /// 단편 셰이더(Fragment Shader)의 GLSL 소스 코드입니다.
        /// 테셀레이션 파이프라인을 거쳐 도출된 최종 픽셀의 색상을 결정하며, 현재는 와이어프레임 구조를 명확히 보기 위해 파란색 단색으로 출력합니다.
        /// </summary>
        private const string FragmentShaderSource =
           "#version 410 core\n" +
            "layout (location = 0) out vec4 FragColor;\n" +
            "in vec3 vNormal;\n" +
            "uniform sampler2D earthTexture;\n" +
            "const float PI = 3.14159265359;\n" +
            "void main() {\n" +
            "    vec3 n = normalize(vNormal);\n" +            
            "    float u = 0.5 + atan(n.z, n.x) / (2.0 * PI);\n" +
            "    float v = 0.5 - asin(n.y) / PI;\n" +
            "    vec2 uv = vec2(u, v);\n" +
            "    \n" +
            "    vec2 dx = dFdx(uv);\n" +
            "    vec2 dy = dFdy(uv);\n" +
            "    \n" +          
            "    if(dx.x > 0.5) dx.x -= 1.0;\n" +
            "    if(dx.x < -0.5) dx.x += 1.0;\n" +
            "    if(dy.x > 0.5) dy.x -= 1.0;\n" +
            "    if(dy.x < -0.5) dy.x += 1.0;\n" +
            "    \n" +          
            "    FragColor = textureGrad(earthTexture, uv, dx, dy);\n" +
            "}\n";

        #endregion

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
            _shader = ShaderCompiler.CreateProgram(
                VertexShaderSource, TcsSource, TesSource, FragmentShaderSource);

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

            GL.UseProgram(_shader);

            // 3. 모델, 뷰, 투영 행렬 전송
            int modelLoc = GL.GetUniformLocation(_shader, "model");
            int viewLoc = GL.GetUniformLocation(_shader, "view");
            int projLoc = GL.GetUniformLocation(_shader, "projection");
            int radiusLoc = GL.GetUniformLocation(_shader, "radius");
            int texLoc = GL.GetUniformLocation(_shader, "earthTexture");

            if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref model); 
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);            
            if (radiusLoc != -1) GL.Uniform1(radiusLoc, Earth.EarthRadius);           
            if (texLoc != -1) GL.Uniform1(texLoc, 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);

            GL.BindVertexArray(_vao);

            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.PatchParameter(PatchParameterInt.PatchVertices, 4);
            GL.DrawElements(PrimitiveType.Patches, _indexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

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
