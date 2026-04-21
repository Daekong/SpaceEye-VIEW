using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.CelestialDefinition
{
    /// <summary>
    /// 3차원 지구 도시를 위한 값을 정의합니다.
    /// </summary>
    static public class Earth
    {
        /// <summary>
        /// 지구 반지름 (Km)
        /// </summary>
        static public double EarthRadius { get; } = 6378.137;

        /// <summary>
        /// 실제 지구의 자전 각속도 (Degrees per Second)입니다.
        /// </summary>
        /// <remarks>
        /// 360도 / 86164.1초(항성일) ≒ 0.00417807 deg/s
        /// </remarks>
        static public double EarthRotationSpeedDegPerSec { get; } = 0.00417807462;

        /// <summary>
        /// 카메라 최대 줌 인 거리 (지구 반경 + 10km)
        /// </summary>
        static public double EarthCameraMinDistance { get; } = EarthRadius + 1;

        /// <summary>
        /// 카메라 최대 줌 아웃 거리 (Km)
        /// </summary>
        static public double EarthCameraMaxDistance { get; } = 100000.0;
    }
}
