using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Core.Orbit
{
    /// <summary>
    /// GPU의 std430 메모리 레이아웃과 정확히 일치하도록 설계된 위성 TLE 데이터 구조체입니다.
    /// </summary>
    /// <remarks>
    /// 이 구조체는 Compute Shader의 SSBO(Shader Storage Buffer Object)로 직접 복사(Zero-Copy)됩니다.
    /// FP64(배정밀도) 연산을 위해 <c>double</c> 타입이 사용되며, 메모리 정렬(Padding)을 맞추기 위해
    /// 4바이트인 <c>int</c>와 <c>float</c>를 연속으로 배치하여 8바이트 블록을 구성했습니다.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct OrbitDataForGpu
    {
        /// <summary>위성의 고유 식별자 (Norad ID)입니다. (4 bytes)</summary>
        public int NoradId;

        /// <summary>대기항력 계수(B-Star Drag Term)입니다. (4 bytes, NoradId와 합쳐 8바이트 정렬)</summary>
        public float BStar;

        /// <summary>궤도 경사각(Inclination)입니다. (8 bytes)</summary>
        public double Inclination;

        /// <summary>승교점 적경(Right Ascension of the Ascending Node)입니다. (8 bytes)</summary>
        public double Raan;

        /// <summary>이심률(Eccentricity)입니다. (8 bytes)</summary>
        public double Eccentricity;

        /// <summary>근지점 인수(Argument of Perigee)입니다. (8 bytes)</summary>
        public double ArgPerigee;

        /// <summary>평균 근점각(Mean Anomaly)입니다. (8 bytes)</summary>
        public double MeanAnomaly;

        /// <summary>평균 운동(Mean Motion)입니다. (8 bytes)</summary>
        public double MeanMotion;
    }
}
