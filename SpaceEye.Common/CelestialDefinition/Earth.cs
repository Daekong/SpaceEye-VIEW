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
        /// 지구 반지름
        /// </summary>
        static public double EarthRadius { get; } = 6378.137;
    }
}
