using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Core.Helpers
{
    /// <summary>
    /// 카메라의 시야 공간(View-Projection)을 6개의 3D 평면으로 추출하여, 
    /// 특정 객체가 화면 안에 있는지 판별하는 절두체 선별 도우미 클래스입니다.
    /// </summary>
    internal class FrustumCuller
    {
        private Vector4d[] _planes = new Vector4d[6];

        /// <summary>
        /// 뷰-투영 행렬을 받아 6개의 절두체 평면을 계산합니다.
        /// </summary>
        /// <param name="vpMatrix">카메라의 ViewMatrix * ProjectionMatrix 결과입니다.</param>
        public FrustumCuller(Matrix4d vpMatrix)
        {
            // OpenTK Matrix4d 행 벡터를 이용해 평면 방정식(Ax + By + Cz + D = 0) 추출
            // OpenTK의 Row/Column 모호함을 피하기 위해, 행렬의 개별 요소(M)를 직접 교차 추출합니다.
            _planes[0] = NormalizePlane(new Vector4d(vpMatrix.M14 + vpMatrix.M11, vpMatrix.M24 + vpMatrix.M21, vpMatrix.M34 + vpMatrix.M31, vpMatrix.M44 + vpMatrix.M41)); // Left
            _planes[1] = NormalizePlane(new Vector4d(vpMatrix.M14 - vpMatrix.M11, vpMatrix.M24 - vpMatrix.M21, vpMatrix.M34 - vpMatrix.M31, vpMatrix.M44 - vpMatrix.M41)); // Right
            _planes[2] = NormalizePlane(new Vector4d(vpMatrix.M14 + vpMatrix.M12, vpMatrix.M24 + vpMatrix.M22, vpMatrix.M34 + vpMatrix.M32, vpMatrix.M44 + vpMatrix.M42)); // Bottom
            _planes[3] = NormalizePlane(new Vector4d(vpMatrix.M14 - vpMatrix.M12, vpMatrix.M24 - vpMatrix.M22, vpMatrix.M34 - vpMatrix.M32, vpMatrix.M44 - vpMatrix.M42)); // Top
            _planes[4] = NormalizePlane(new Vector4d(vpMatrix.M14 + vpMatrix.M13, vpMatrix.M24 + vpMatrix.M23, vpMatrix.M34 + vpMatrix.M33, vpMatrix.M44 + vpMatrix.M43)); // Near
            _planes[5] = NormalizePlane(new Vector4d(vpMatrix.M14 - vpMatrix.M13, vpMatrix.M24 - vpMatrix.M23, vpMatrix.M34 - vpMatrix.M33, vpMatrix.M44 - vpMatrix.M43)); // Far
        }

        private Vector4d NormalizePlane(Vector4d plane)
        {
            // 평면의 법선 벡터(X, Y, Z) 길이만 구합니다. (W는 거리를 나타내므로 길이에 포함하지 않음)
            double length = Math.Sqrt(plane.X * plane.X + plane.Y * plane.Y + plane.Z * plane.Z);

            // X, Y, Z 정규화 및 W(원점에서의 거리)도 동일한 비율로 축소해야 정확한 거리 계산이 가능합니다.
            return new Vector4d(plane.X / length, plane.Y / length, plane.Z / length, plane.W / length);
        }

        /// <summary>
        /// 주어진 구(Sphere) 영역이 카메라 시야(Frustum) 안에 있는지 검사합니다.
        /// </summary>
        /// <param name="center">구의 중심 절대 좌표입니다.</param>
        /// <param name="radius">구의 반지름입니다.</param>
        /// <returns>시야 안에 있거나 걸쳐있으면 true, 완전히 밖에 있으면 false를 반환합니다.</returns>
        public bool IntersectsSphere(Vector3d center, double radius)
        {
            for (int i = 0; i < 6; i++)
            {
                // 평면 방정식 (Ax + By + Cz + D) 계산
                double distance = _planes[i].X * center.X + _planes[i].Y * center.Y + _planes[i].Z * center.Z + _planes[i].W;

                // 중심점이 평면 반대 방향으로 반지름보다 멀리 떨어져 있으면 시야 밖입니다.
                if (distance < -radius) return false;
            }
            return true;
        }
    }
}
