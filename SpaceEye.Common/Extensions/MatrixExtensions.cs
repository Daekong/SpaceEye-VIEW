using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Extensions
{
    public static class MatrixExtensions
    {
        /// <summary>
        /// Matrix4d(64비트)를 Matrix4(32비트)로 변환하는 프라이빗 메서드입니다.
        /// </summary>
        /// <param name="d">원본 64비트 행렬입니다.</param>
        /// <returns>변환된 32비트 행렬입니다.</returns>
        public static Matrix4 ToMatrix4(this Matrix4d d)
        {
            return new Matrix4(
                (float)d.M11, (float)d.M12, (float)d.M13, (float)d.M14,
                (float)d.M21, (float)d.M22, (float)d.M23, (float)d.M24,
                (float)d.M31, (float)d.M32, (float)d.M33, (float)d.M34,
                (float)d.M41, (float)d.M42, (float)d.M43, (float)d.M44);
        }
    }
}
