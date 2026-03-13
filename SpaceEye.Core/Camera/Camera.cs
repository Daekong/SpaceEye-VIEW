using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using OpenTK;

namespace SpaceEye.Core.Camera
{
    /// <summary>
    /// 3D 가상 우주 공간을 관찰하는 카메라 클래스입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 모든 좌표와 변환 행렬은 <see cref="Double"/> 타입을 사용합니다.   
    /// </para>
    /// <para>
    /// 단위 체계는 킬로미터(km)를 사용하며, OpenGL의 오른손 좌표계를 따릅니다.
    /// </para>
    /// </remarks>
    internal class Camera
    {
        /// <summary>카메라의 현재 위치 좌표 (단위: km)입니다.</summary>
        public Vector3d Position { get; set; } = new Vector3d(0, 0, 20000.0);

        /// <summary>카메라가 바라보는 대상의 중심 좌표 (단위: km)입니다.</summary>
        public Vector3d Target { get; set; } = Vector3d.Zero;

        /// <summary>카메라의 상단 방향 벡터입니다. 기본값은 Y축 방향입니다.</summary>
        public Vector3d Up { get; set; } = Vector3d.UnitY;

        /// <summary>수직 시야각 (Field of View, 단위: Degree)입니다.</summary>
        public double Fov { get; set; } = 45.0f;

        /// <summary>화면의 종횡비 (너비 / 높이)입니다.</summary>
        public double AspectRatio { get; set; } = 1.0f;

        /// <summary>카메라가 렌더링을 시작하는 최소 거리 (Near Clip Plane, 단위: km)입니다.</summary>
        public double Near { get; set; } = 0.1;

        /// <summary>카메라가 렌더링을 수행하는 최대 거리 (Far Clip Plane, 단위: km)입니다.</summary>
        /// <remarks>지구와 달의 거리를 고려하여 충분히 큰 값(예: 1,000,000km)으로 설정합니다.</remarks>
        public double Far { get; set; } = 2000000.0;

        /// <summary>
        /// <see cref="Camera"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        internal Camera()
        {
        }

        /// <summary>
        /// 현재 카메라의 위치와 대상 정보를 바탕으로 64비트 뷰 행렬(View Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 행렬을 반환합니다.</returns>
        public Matrix4d GetViewMatrix()
        {
            // LookAt 연산을 64비트로 수행하여 정밀도를 유지합니다.
            return Matrix4d.LookAt(Position, Target, Up);
        }

        /// <summary>
        /// 현재 카메라의 시야각 및 클리핑 평면 정보를 바탕으로 64비트 투영 행렬(Projection Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 투영 행렬을 반환합니다.</returns>
        public Matrix4d GetProjectionMatrix()
        {
            // FOV를 라디안으로 변환하여 원근 투영 행렬 생성
            return Matrix4d.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                AspectRatio,
                Near,
                Far);
        }

        /// <summary>
        /// 카메라를 대상(Target)을 중심으로 특정 각도만큼 회전시킵니다. (Orbit 기능용)
        /// </summary>
        /// <param name="horizontalAngle">수평 회전 각도 (단위: Radian)</param>
        /// <param name="verticalAngle">수직 회전 각도 (단위: Radian)</param>
        public void RotateOrbit(double horizontalAngle, double verticalAngle)
        {
            // 이 메서드는 이후 마우스 핸들러 구현 시 
            // 구면 좌표계 변환을 통해 카메라 위치를 갱신하는 데 사용됩니다.
        }
    }
}