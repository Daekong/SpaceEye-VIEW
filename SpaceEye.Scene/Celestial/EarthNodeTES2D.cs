using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Enums;
using SpaceEye.Common.Interfaces;
using SpaceEye.Common.Scene;
using SpaceEye.Core.Common;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Scene.Celestial
{
    /// <summary>
    /// 2D 직교 투영 환경에서 평면 지구 지도를 렌더링하는 노드 클래스입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 테셀레이션을 사용하여 평면을 세밀하게 분할하며, 무한 스핀(Infinity Spin) 기능을 통해 
    /// 지도가 동서 방향으로 끊임없이 반복되도록 처리합니다.
    /// </para>
    /// </remarks>
    internal class EarthNodeTES2D : ISceneNode, ITimeUpdateable, IDisposable
    {
        #region # Private Fields

        /// <summary>Vertex Array Object 식별자입니다.</summary>
        private int _vao;

        /// <summary>Vertex Buffer Object 식별자입니다.</summary>
        private int _vbo;

        /// <summary>Element Buffer Object 식별자입니다.</summary>
        private int _ebo;

        /// <summary>셰이더 프로그램 식별자입니다.</summary>
        private int _shader;

        /// <summary>지구 지도 텍스처 식별자입니다.</summary>
        private int _texture;

        /// <summary>지구의 현재 자전 상태를 반영하는 누적 오프셋 값입니다.</summary>
        private double _rotationOffset = 0.0;

        /// <summary>카메라 이동에 따른 무한 스핀 보정 오프셋입니다.</summary>
        private float _spinOffset = 0.0f;

        /// <summary>평면 구성을 위한 정점 데이터입니다. (X, Y, Z, U, V)</summary>
        private readonly double[] _vertices = {
            // 위치 (X, Y, Z)               // UV (U, V)
            -Earth.EarthRadius * Math.PI, -Earth.EarthRadius * (Math.PI / 2.0), 0.0,  0.0, 1.0, // 좌하단
             Earth.EarthRadius * Math.PI, -Earth.EarthRadius * (Math.PI / 2.0), 0.0,  1.0, 1.0, // 우하단
             Earth.EarthRadius * Math.PI,  Earth.EarthRadius * (Math.PI / 2.0), 0.0,  1.0, 0.0, // 우상단
            -Earth.EarthRadius * Math.PI,  Earth.EarthRadius * (Math.PI / 2.0), 0.0,  0.0, 0.0  // 좌상단
        };

        /// <summary>사각형 구성을 위한 인덱스 데이터입니다.</summary>
        private readonly uint[] _indices = { 0, 1, 2, 3 };

        #endregion

        #region # Properties

        /// <summary>현재 노드의 투영 모드를 가져옵니다. 2D 전용이므로 Orthographic을 반환합니다.</summary>
        public ProjectionMode ProjectionMode => ProjectionMode.Orthographic;

        #endregion

        #region # Constructor & Initialize

        /// <summary>
        /// <see cref="EarthNodeTES2D"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        internal EarthNodeTES2D() { }

        /// <summary>
        /// 2D 렌더링에 필요한 자원(셰이더, 버퍼, 텍스처)을 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            string vertexShaderSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "VertexShaders", "Earth2D.vert");
            string tcsSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "TCSShaders", "Earth2D.tcs");
            string tesSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "TESShaders", "Earth2D.tes");
            string fragmentShaderSource = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Shaders", "Celestials", "FragmentShaders", "Earth2D.frag"); ;

            _shader = ShaderCompiler.CreateProgramFromFiles(vertexShaderSource, tcsSource, tesSource, fragmentShaderSource);

            // 예: 8K 해상도의 일반적인 평면 세계지도 이미지 파일
            // 1. Resource DLL의 이름 (예: SpaceEye.Resources)
            string basePath = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Resouces", "Images", "BaseMap");
            _texture = TextureLoader.LoadTexture(Path.Combine(basePath, "Earth_BlueMarble_NextGeneration_2Km.jpg"));
            
            // 무한 스핀을 위해 텍스처 래핑을 REPEAT로 설정
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(double), _vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            // 위치 속성 (Location 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribLPointer(0, 3, VertexAttribDoubleType.Double, 5 * sizeof(double), IntPtr.Zero);

            // UV 속성 (Location 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribLPointer(1, 2, VertexAttribDoubleType.Double, 5 * sizeof(double), new IntPtr(3 * sizeof(double)));

            GL.BindVertexArray(0);
        }

        #endregion

        #region # Update & Draw

        /// <summary>
        /// 지구의 자전 및 카메라 위치에 따른 무한 스핀 오프셋을 업데이트합니다.
        /// </summary>
        /// <param name="deltaSeconds">경과 시간입니다.</param>
        /// <param name="camera">현재 시점의 카메라 인터페이스입니다.</param>
        public void Update(double deltaSeconds, ICamera camera)
        {
            // 1. 자전에 의한 기본 회전 오프셋 (0.0 ~ 1.0)
            double speedFactor = Earth.EarthRotationSpeedDegPerSec / 360.0;
            _rotationOffset += speedFactor * deltaSeconds;
            _rotationOffset %= 1.0;

            // 2. Infinity Spin: 카메라의 X 위치(km)를 지도의 전체 너비로 나누어 UV 오프셋으로 변환
            // 지도 너비 = 2 * PI * Radius
            double mapWidthKm = 2.0 * Math.PI * Earth.EarthRadius;
            double cameraXOffset = camera.Position.X / mapWidthKm;

            // 최종 셰이더에 전달할 오프셋 (자전 + 카메라 이동)
            _spinOffset = (float)((_rotationOffset + cameraXOffset) % 1.0);
        }

        /// <summary>
        /// 2D 평면 지구를 렌더링합니다.
        /// </summary>
        /// <param name="view">뷰 행렬입니다.</param>
        /// <param name="projection">투영 행렬입니다.</param>
        /// <param name="renderMode">현재 렌더링 모드입니다.</param>
        public void Draw(Matrix4d view, Matrix4d projection, ProjectionMode renderMode)
        {
            GL.UseProgram(_shader);

            // 2D에서는 모델 행렬을 기본값으로 사용하거나 필요 시 위치 조정
            Matrix4d model = Matrix4d.Identity;

            int modelLoc = GL.GetUniformLocation(_shader, "model");
            int viewLoc = GL.GetUniformLocation(_shader, "view");
            int projLoc = GL.GetUniformLocation(_shader, "projection");
            int offsetLoc = GL.GetUniformLocation(_shader, "uOffset");
            int isWireframeLoc = GL.GetUniformLocation(_shader, "isWireframe");
            int wireColorLoc = GL.GetUniformLocation(_shader, "wireColor");

            if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref model);
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);
            if (offsetLoc != -1) GL.Uniform1(offsetLoc, _spinOffset);
            if (isWireframeLoc != -1) GL.Uniform1(isWireframeLoc, UniverseScene.Instance.IsWireframe ? 1 : 0);
            if (wireColorLoc != -1) GL.Uniform3(wireColorLoc, 1.0f, 1.0f, 0.0f); // Yellow

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);

            GL.BindVertexArray(_vao);
            GL.PatchParameter(PatchParameterInt.PatchVertices, 4);
            GL.DrawElements(PrimitiveType.Patches, 4, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        #endregion

        #region # IDisposable

        /// <summary>
        /// OpenGL 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteProgram(_shader);
            GL.DeleteTexture(_texture);
        }

        #endregion
    }
}
