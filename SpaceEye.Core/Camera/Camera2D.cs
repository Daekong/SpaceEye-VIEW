using OpenTK;
using SpaceEye.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Core.Camera
{
    /// <summary>
    /// 2D 평면 또는 직교 투영(Orthographic) 기반의 가상 공간을 관찰하는 카메라 클래스입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 3D의 원근 투영과 달리 거리와 관계없이 사물의 크기가 일정하게 유지되는 직교 투영 방식을 사용합니다.
    /// </para>
    /// <para>
    /// 단위 체계는 킬로미터(km)를 사용하며, 64비트 부동 소수점 정밀도를 지원합니다.
    /// </para>
    /// </remarks>
    internal class Camera2D : ICamera
    {
        #region # Fields

        /// <summary>
        /// 화면의 세로 방향으로 보여질 실제 물리적 거리(단위: km)입니다.
        /// </summary>
        /// <remarks>
        /// 이 값이 작을수록 화면이 확대(Zoom In)되고, 클수록 축소(Zoom Out)됩니다.
        /// </remarks>
        private double _orthoVerticalSize = 1000.0;

        #endregion

        #region # Properties

        /// <summary>
        /// 카메라의 현재 위치 좌표 (단위: km)를 가져오거나 설정합니다.
        /// </summary>
        /// <value>기본값은 (0, 0, 100)이며, Z값은 렌더링 레이어 순서 결정에 사용됩니다.</value>
        public Vector3d Position { get; set; } = new Vector3d(0, 0, 100.0);

        /// <summary>
        /// 카메라가 바라보는 대상의 중심 좌표 (단위: km)를 가져오거나 설정합니다.
        /// </summary>
        public Vector3d Target { get; set; } = Vector3d.Zero;

        /// <summary>
        /// 카메라의 상단 방향 벡터를 가져오거나 설정합니다.
        /// </summary>
        /// <value>기본값은 Y축 방향인 (0, 1, 0)입니다.</value>
        public Vector3d Up { get; set; } = Vector3d.UnitY;

        /// <summary>
        /// 2D 환경에서의 시야 범위(줌 레벨)를 가져오거나 설정합니다.
        /// </summary>
        /// <remarks>
        /// <see cref="ICamera"/> 인터페이스의 호환성을 위해 Fov 이름을 사용하며, 내부적으로는 화면 세로 높이(km)를 조절합니다.
        /// </remarks>
        public double Fov
        {
            get => _orthoVerticalSize;
            set => _orthoVerticalSize = Math.Max(0.001, value);
        }

        /// <summary>화면의 너비(Pixel)를 가져오거나 설정합니다.</summary>
        public double ScreenWidth { get; set; } = 1920.0;

        /// <summary>화면의 높이(Pixel)를 가져오거나 설정합니다.</summary>
        public double ScreenHeight { get; set; } = 1080.0;

        /// <summary>화면의 종횡비 (너비 / 높이)를 가져오거나 설정합니다.</summary>
        public double AspectRatio { get; set; } = 1.0;

        /// <summary>전면 클리핑 평면(Near Clip Plane, 단위: km)을 가져오거나 설정합니다.</summary>
        public double Near { get; set; } = 0.1;

        /// <summary>후면 클리핑 평면(Far Clip Plane, 단위: km)을 가져오거나 설정합니다.</summary>
        public double Far { get; set; } = 10000.0;

        #endregion

        /// <summary>
        /// <see cref="Camera2D"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        internal Camera2D()
        {
        }

        /// <summary>
        /// 현재 카메라의 위치와 대상 정보를 바탕으로 64비트 뷰 행렬(View Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 뷰 행렬입니다.</returns>
        public Matrix4d GetViewMatrix()
        {
            return Matrix4d.LookAt(Position, Target, Up);
        }

        /// <summary>
        /// 현재 설정된 시야 범위 및 종횡비를 바탕으로 64비트 직교 투영 행렬(Orthographic Projection Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 직교 투영 행렬입니다.</returns>
        public Matrix4d GetProjectionMatrix()
        {
            double halfHeight = _orthoVerticalSize / 2.0;
            double halfWidth = halfHeight * AspectRatio;

            return Matrix4d.CreateOrthographicOffCenter(
                -halfWidth,
                 halfWidth,
                -halfHeight,
                 halfHeight,
                 Near,
                 Far);
        }

        /// <summary>
        /// 마우스 휠 변화량에 따라 카메라의 시야 범위(Zoom)를 조절합니다.
        /// </summary>
        /// <param name="delta">마우스 휠 스크롤 변화량입니다.</param>
        public void Zoom(double delta)
        {
            double zoomFactor = _orthoVerticalSize * 0.1;

            if (delta > 0)
                _orthoVerticalSize -= zoomFactor;
            else
                _orthoVerticalSize += zoomFactor;

            _orthoVerticalSize = MathHelper.Clamp(_orthoVerticalSize, 0.001, 1000000.0);
        }

        /// <summary>
        /// 카메라의 위치와 대상 좌표를 동시에 이동시켜 평면 이동(Panning) 효과를 구현합니다.
        /// </summary>
        /// <param name="moveX">X축 이동량 (단위: km)입니다.</param>
        /// <param name="moveY">Y축 이동량 (단위: km)입니다.</param>
        public void Move(double moveX, double moveY)
        {
            Position = new Vector3d(Position.X + moveX, Position.Y + moveY, Position.Z);
            Target = new Vector3d(Target.X + moveX, Target.Y + moveY, Target.Z);
        }
    }
}
