using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.Interfaces;
using SpaceEye.Core.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Renderer
{
    /// <summary>
    /// 우주 씬을 2D 직교 투영 방식으로 렌더링하는 전용 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 깊이 테스트보다는 객체의 레이어 순서 및 알파 블렌딩 처리에 최적화되어 있습니다.
    /// </remarks>
    internal class SceneRenderer2D
    {
        /// <summary>
        /// 이 렌더러에서 사용하는 2D 직교 투영 카메라 인스턴스입니다.
        /// </summary>
        public Camera2D ViewCamera { get; } = new Camera2D();

        /// <summary>
        /// <see cref="SceneRenderer2D"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        internal SceneRenderer2D()
        {
        }

        /// <summary>
        /// 지정된 씬을 현재 카메라 시점과 해상도에 맞춰 2D 방식으로 렌더링합니다.
        /// </summary>
        /// <param name="scene">렌더링할 <see cref="IUniverseScene"/> 객체입니다.</param>
        /// <param name="width">렌더링 영역의 너비(Pixel)입니다.</param>
        /// <param name="height">렌더링 영역의 높이(Pixel)입니다.</param>
        public void Render(IUniverseScene scene, double width, double height)
        {
            if (scene == null || width <= 0 || height <= 0) return;

            // 1. 뷰포트 업데이트
            GL.Viewport(0, 0, (int)width, (int)height);
            ViewCamera.ScreenWidth = width;
            ViewCamera.ScreenHeight = height;
            ViewCamera.AspectRatio = width / height;

            // 2. 버퍼 초기화
            GL.ClearColor(0.02f, 0.02f, 0.05f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // 3. 2D 상태 설정
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            // 4. 행렬 계산 및 렌더링 수행
            var view = ViewCamera.GetViewMatrix();
            var projection = ViewCamera.GetProjectionMatrix();

            scene.RenderAll(view, projection, Common.Enums.ProjectionMode.Orthographic);

            // 5. 상태 복구
            GL.Disable(EnableCap.Blend);
        }
    }
}
