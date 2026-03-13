using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Core.Celestial
{
    /// <summary>
    /// Km 단위를 사용하여 구체 메쉬 데이터를 생성하는 유틸리티 클래스입니다.
    /// </summary>
    internal static class SphereGenerator
    {

        // <summary>
        /// 위경도 분할 수를 바탕으로 구체의 정점 좌표와 텍스처 좌표를 생성합니다.
        /// </summary>
        /// <param name="radiusKm">구체의 반지름 (단위: km, 지구 평균 약 6371.0f)</param>
        /// <param name="segments">구체의 세밀도 (보통 64 이상 권장)</param>
        /// <returns>생성된 정점 데이터(vertices)와 인덱스 데이터(indices)를 포함하는 튜플을 반환합니다.</returns>
        internal static (double[] vertices, uint[] indices) GenerateSphere(double radiusKm, int segments)
        {
            // 정점 하나당 (X, Y, Z, U, V) 5개의 float 사용
            List<double> vList = new List<double>();
            List<uint> iList = new List<uint>();

            for (int y = 0; y <= segments; y++)
            {
                // 위도(phi): 0(북극) ~ PI(남극)
                double phi = (y * Math.PI / segments);
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                for (int x = 0; x <= segments; x++)
                {
                    // 경도(theta): 0 ~ 2PI
                    double theta = (x * 2.0 * Math.PI / segments);
                    double sinTheta = Math.Sin(theta);
                    double cosTheta = Math.Cos(theta);

                    // 1. Km 기준 직교 좌표 계산 (Y-Up 가동)
                    // OpenGL 관례에 따라 Y축을 위쪽(북극 방향)으로 설정
                    double vx = radiusKm * sinPhi * Math.Cos(theta);
                    double vy = radiusKm * cosPhi;
                    double vz = radiusKm * sinPhi * Math.Sin(theta);

                    // 2. UV 좌표 (지도 텍스처 매핑용)
                    double u = x / segments;
                    double v = y / segments;

                    vList.Add(vx); vList.Add(vy); vList.Add(vz);
                    vList.Add(u); vList.Add(v);
                }
            }

            // 3. 인덱스 생성 (CCW: 반시계 방향 감기)
            for (int y = 0; y < segments; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    uint first = (uint)((y * (segments + 1)) + x);
                    uint second = first + (uint)segments + 1;

                    // 삼각형 1
                    iList.Add(first);
                    iList.Add(second);
                    iList.Add(first + 1);

                    // 삼각형 2
                    iList.Add(second);
                    iList.Add(second + 1);
                    iList.Add(first + 1);
                }
            }

            // 결과를 튜플 형태로 즉시 반환
            return (vList.ToArray(), iList.ToArray());
        }
    }
}