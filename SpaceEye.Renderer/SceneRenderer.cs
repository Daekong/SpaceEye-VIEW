using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.Interfaces;
using SpaceEye.Common.Scene;
using SpaceEye.Core.Camera;
using SpaceEye.Scene;

namespace SpaceEye.Renderer
{
    /// <summary>
    /// 주입받은 우주 씬(<see cref="UniverseScene"/>)을 개별 카메라 시점으로 렌더링하는 내부 전용 클래스입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 각 <c>EarthControl</c>은 자신만의 <see cref="SceneRenderer"/> 인스턴스를 소유하며, 
    /// 이를 통해 동일한 씬을 서로 다른 위치와 각도에서 관찰할 수 있는 Multi-View 구조를 구현합니다.
    /// </para>
    /// <para>
    /// 모든 시점 계산은 64비트 정밀도(<see cref="Matrix4d"/>)로 수행되어 거대 스케일에서의 정밀도를 보장합니다.
    /// </para>
    /// </remarks>
    internal class SceneRenderer
    {
        /// <summary>
        /// 화면(PIP) 전용 마스크
        /// </summary>
        private int _visibilitySsbo;

        /// <summary>
        /// 이 렌더러가 관리하는 고유 카메라 인스턴스입니다.
        /// </summary>
        /// <remarks>
        /// 외부(UI 조작 로직 등)에서 이 카메라의 Position이나 Target을 변경하여 시점을 제어할 수 있습니다.
        /// </remarks>
        public Camera ViewCamera { get; } = new Camera();

        /// <summary>
        /// <see cref="SceneRenderer"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        internal SceneRenderer()
        {
        }

        /// <summary>
        /// 주어진 우주 씬을 현재 뷰포트 크기에 맞춰 64비트 정밀도로 렌더링합니다.
        /// </summary>
        /// <param name="scene">렌더링할 통합 우주 씬 객체입니다.</param>
        /// <param name="width">현재 렌더링 영역의 너비(Pixel)입니다.</param>
        /// <param name="height">현재 렌더링 영역의 높이(Pixel)입니다.</param>
        /// <remarks>
        /// 1. 뷰포트 크기에 따라 카메라의 종횡비(Aspect Ratio)를 자동 업데이트합니다. <br/>
        /// 2. 카메라로부터 64비트 뷰 및 투영 행렬을 계산하여 씬에 전달합니다.
        /// </remarks>
        public void Render(IUniverseScene scene, double width, double height)
        {
            if (scene == null || width <= 0 || height <= 0) return;

            // 1. 뷰포트 및 카메라 설정 (한 번만 호출)
            GL.Viewport(0, 0, (int)width, (int)height);
            ViewCamera.ScreenWidth = width;
            ViewCamera.ScreenHeight = height;
            ViewCamera.AspectRatio = width / height;

            // 2. 화면 비우기 (짙은 남색 배경)
            GL.ClearColor(0.05f, 0.05f, 0.1f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // 3. 기본 상태 설정
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            // ❌ 여기서 PolygonMode.Line을 설정하지 마세요! 
            // 기본값인 Fill 상태로 두어 스카이박스가 정상적으로 면으로 그려지게 합니다.
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            // 카메라 행렬 가져오기
            var view = ViewCamera.GetViewMatrix();
            var projection = ViewCamera.GetProjectionMatrix();

            // ★ 핵심: 우주를 그리기 직전에 내 화면 전용 Visibility 버퍼를 2번 슬롯에 바인딩
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _visibilitySsbo);            

            // 씬 그리기 (내부에서 스카이박스와 지구가 각각 그려짐)
            scene.RenderAll(view, projection);
        }
    }
}