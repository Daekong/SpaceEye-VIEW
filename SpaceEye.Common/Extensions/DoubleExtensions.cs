using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Extensions
{
    /// <summary>
    /// 배정밀도 부동 소수점(double) 수치를 위한 각도 변환 확장 메서드 클래스입니다.
    /// </summary>
    public static class DoubleExtensions
    {
        /// <summary>
        /// 라디안(Radian) 단위를 도(Degree) 단위로 변환합니다.
        /// </summary>
        /// <param name="radians">변환할 라디안 값입니다.</param>
        /// <returns>변환된 도(Degree) 값입니다.</returns>
        public static double ToDegree(this double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        /// <summary>
        /// 도(Degree) 단위를 라디안(Radian) 단위로 변환합니다.
        /// </summary>
        /// <param name="degrees">변환할 도(Degree) 값입니다.</param>
        /// <returns>변환된 라디안(Radian) 값입니다.</returns>
        public static double ToRadian(this double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }
    }
}
