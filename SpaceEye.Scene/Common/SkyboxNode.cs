using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.Enums;
using SpaceEye.Core.Common;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Resources;

namespace SpaceEye.Scene.Common
{   
    /// <summary>
    /// 우주 배경(스카이박스)을 렌더링하기 위한 씬 노드 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 이 클래스는 64비트 정밀도(fp64)를 활용하여 무한히 먼 거리에 있는 배경을 렌더링합니다.
    /// 카메라의 이동(Translation)에는 영향을 받지 않고 오직 회전(Rotation)에만 반응하여, 
    /// 사용자가 광활한 우주 공간 중심에 있는 듯한 시각적 효과를 제공합니다.
    /// </remarks>
    internal class SkyboxNode : ISceneNode, IDisposable
    {
        #region # Fields

        /// <summary>
        /// 정점 배열 객체(Vertex Array Object)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vao;

        /// <summary>
        /// 정점 버퍼 객체(Vertex Buffer Object)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vbo;

        /// <summary>
        /// 스카이박스 렌더링 전용 셰이더 프로그램의 OpenGL 식별자입니다.
        /// </summary>
        private int _shader;

        /// <summary>
        /// 6면의 우주 이미지가 하나로 결합된 큐브맵 텍스처(Cubemap Texture)의 OpenGL 식별자입니다.
        /// </summary>
        private int _texture;

        /// <summary>
        /// 스카이박스를 구성하는 정육면체의 36개 정점 데이터 배열입니다. (배정밀도 부동 소수점)
        /// </summary>
        /// <remarks>
        /// 6개의 면(Face)을 각각 2개의 삼각형(총 6개 정점)으로 구성하여 총 36개의 정점을 가집니다.
        /// 카메라는 항상 이 큐브의 내부(원점)에 위치하여 안에서 밖을 바라보게 됩니다.
        /// </remarks>
        private readonly float[] _vertices = {
            // Back face
            -1.0f,  1.0f, -1.0f,  -1.0f, -1.0f, -1.0f,   1.0f, -1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,   1.0f,  1.0f, -1.0f,  -1.0f,  1.0f, -1.0f,
            // Left face
            -1.0f, -1.0f,  1.0f,  -1.0f, -1.0f, -1.0f,  -1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,  -1.0f,  1.0f,  1.0f,  -1.0f, -1.0f,  1.0f,
            // Right face
             1.0f, -1.0f, -1.0f,   1.0f, -1.0f,  1.0f,   1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,   1.0f,  1.0f, -1.0f,   1.0f, -1.0f, -1.0f,
            // Front face
            -1.0f, -1.0f,  1.0f,  -1.0f,  1.0f,  1.0f,   1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,   1.0f, -1.0f,  1.0f,  -1.0f, -1.0f,  1.0f,
            // Bottom face
            -1.0f, -1.0f, -1.0f,   1.0f, -1.0f, -1.0f,   1.0f, -1.0f,  1.0f,
             1.0f, -1.0f,  1.0f,  -1.0f, -1.0f,  1.0f,  -1.0f, -1.0f, -1.0f,
            // Top face
            -1.0f,  1.0f, -1.0f,  -1.0f,  1.0f,  1.0f,   1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,   1.0f,  1.0f, -1.0f,  -1.0f,  1.0f, -1.0f
        };

        #endregion

        #region # Properties

        /// <summary>
        ///     프로젝션 모드 입니다.
        /// </summary>
        public ProjectionMode ProjectionMode { get; } = ProjectionMode.Perspective;

        #endregion

        #region # Shader Files
        // 스카이박스용 셰이더 소스 (내장) - 32비트 Float
        private const string VertexShaderSource =
            "#version 430 core\n" +
            "layout (location = 0) in vec3 aPos;\n" +
            "out vec3 TexCoords;\n" +
            "uniform mat4 view;\n" +
            "uniform mat4 projection;\n" +
            "void main()\n" +
            "{\n" +
            "    TexCoords = aPos;\n" +
            "    vec4 pos = projection * view * vec4(aPos, 1.0);\n" +
            "    gl_Position = pos.xyww;\n" +
            "}\n";

        private const string FragmentShaderSource =
            "#version 430 core\n" +
            "layout (location = 0) out vec4 FragColor;\n" +
            "in vec3 TexCoords;\n" +
            "uniform samplerCube skybox;\n" +
            "void main()\n" +
            "{\n" +
            "    FragColor = texture(skybox, TexCoords);\n" +
            "}\n";

        #endregion

        #region # Constructor

        /// <summary>
        /// <see cref="SkyboxNode"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <remarks>접근 제한자가 internal로 설정되어 엔진 외부에서의 직접 생성을 차단합니다.</remarks>
        public SkyboxNode()
        {

        }

        #endregion

        #region # Public Methods

        /// <summary>
        /// 스카이박스 렌더링에 필요한 GPU 리소스(VAO, VBO, 셰이더, 텍스처)를 초기화합니다.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>64비트 정점 데이터를 GPU에 전달하기 위해 <see cref="GL.VertexAttribLPointer"/>를 사용합니다.</description></item>
        /// <item><description>호출 전 유효한 OpenGL 컨텍스트가 활성화되어 있어야 합니다.</description></item>
        /// </list>
        /// </remarks>
        public void Initialize()
        {
            // 1. 셰이더 생성 (410 core 버전)
             _shader = ShaderCompiler.CreateProgram(VertexShaderSource, FragmentShaderSource);

            // 2. VAO/VBO 생성 및 64비트 데이터 레이아웃 설정
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // CPU의 float[] 데이터를 GPU 메모리에 직접 복사
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            // 일반적인 Float 포인터 사용
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // 1. Resource DLL의 이름 (예: SpaceEye.Resources)
            string basePath = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Resouces", "Images", "Skybox");

            // 2. 6면 이미지 경로 설정 (순서 주의: 우, 좌, 상, 하, 전, 후)
            string[] resources = {
                    Path.Combine(basePath , "Right.png"),
                    Path.Combine(basePath , "Left.png"),
                    Path.Combine(basePath , "Top.png"),
                    Path.Combine(basePath , "Bottom.png"),
                    Path.Combine(basePath , "Front.png"),
                    Path.Combine(basePath , "Back.png"),
                    };
            
            // 3. 생성된 Bitmap 배열을 TextureLoader로 넘겨 GPU에 업로드
            _texture = TextureLoader.LoadCubemap(resources);
        }

        /// <summary>
        /// 카메라의 현재 시점을 기준으로 스카이박스를 렌더링합니다.
        /// </summary>
        /// <param name="view">카메라의 뷰 행렬(View Matrix)입니다.</param>
        /// <param name="projection">카메라의 투영 행렬(Projection Matrix)입니다.</param>
        /// <param name="renderMode">현재 Scene의 ProjectionMode</param>
        /// <remarks>
        /// 뷰 행렬에서 이동(Translation) 성분을 강제로 제거하여 배경이 항상 카메라와 동일한 거리를 유지하도록 합니다.
        /// 또한, 배경이 항상 다른 객체들의 뒤에 그려지도록 깊이 테스트 함수를 <see cref="DepthFunction.Lequal"/>로 임시 변경합니다.
        /// </remarks>
        public void Draw(Matrix4d view, Matrix4d projection, ProjectionMode renderMode)
        {
            // [중요: 다운캐스팅] CPU의 64비트(Matrix4d)를 GPU가 받을 수 있게 32비트(Matrix4)로 변환
            // 동시에 카메라의 이동(Translation) 성분을 0으로 만들어 우주가 따라오게 합니다.
            Matrix4 viewFloat = new Matrix4(
                (float)view.Row0.X, (float)view.Row0.Y, (float)view.Row0.Z, 0.0f,
                (float)view.Row1.X, (float)view.Row1.Y, (float)view.Row1.Z, 0.0f,
                (float)view.Row2.X, (float)view.Row2.Y, (float)view.Row2.Z, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f
            );

            Matrix4 projFloat = new Matrix4(
                (float)projection.Row0.X, (float)projection.Row0.Y, (float)projection.Row0.Z, (float)projection.Row0.W,
                (float)projection.Row1.X, (float)projection.Row1.Y, (float)projection.Row1.Z, (float)projection.Row1.W,
                (float)projection.Row2.X, (float)projection.Row2.Y, (float)projection.Row2.Z, (float)projection.Row2.W,
                (float)projection.Row3.X, (float)projection.Row3.Y, (float)projection.Row3.Z, (float)projection.Row3.W
            );

            GL.UseProgram(_shader);

            // Float 행렬 전송 (InvalidOperation 에러의 주범 해결)
            int viewLoc = GL.GetUniformLocation(_shader, "view");
            int projLoc = GL.GetUniformLocation(_shader, "projection");

            if (viewLoc != -1) 
                GL.UniformMatrix4(viewLoc, false, ref viewFloat);
            if (projLoc != -1) 
                GL.UniformMatrix4(projLoc, false, ref projFloat);
        
            // 텍스처 샘플러 바인딩
            int skyboxLoc = GL.GetUniformLocation(_shader, "skybox");
            if (skyboxLoc != -1) 
                GL.Uniform1(skyboxLoc, 0);

            // 렌더링 상태 설정
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal); // Z값이 1.0이어도 통과하도록 설정
            GL.Disable(EnableCap.CullFace);     // 큐브 안쪽 면을 볼 수 있도록 Culling 끄기
            GL.DepthMask(false);                // 스카이박스는 배경이므로 깊이 버퍼에 쓰지 않음

            // 그리기
            GL.BindVertexArray(_vao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, _texture);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            // 상태 원상 복구 (다른 노드 렌더링을 위해)
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);

            GL.BindVertexArray(0);
            GL.UseProgram(0);

        }

        #endregion

        #region # IDisposable Support

        /// <summary>
        /// 스카이박스 렌더링을 위해 할당된 모든 GPU 리소스를 즉시 해제합니다.
        /// </summary>
        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_shader);
            GL.DeleteTexture(_texture);
        }

        #endregion
    }
}
