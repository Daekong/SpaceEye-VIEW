using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Interfaces
{
    /// <summary>
    ///     Camera 정보 인터페이스
    /// </summary>
    public interface ICamera
    {
        /// <summary>수직 시야각 (Field of View, 단위: Degree)입니다.</summary>
        double Fov { get; set; }

        /// <summary>화면의 종횡비 (너비 / 높이)입니다.</summary>
        double ScreenWidth { get; set; }

        /// <summary>화면의 종횡비 (너비 / 높이)입니다.</summary>
        double ScreenHeight { get; set; }

        /// <summary>화면의 종횡비 (너비 / 높이)입니다.</summary>
        double AspectRatio { get; set; }

        /// <summary>카메라의 현재 위치 좌표 (단위: km)입니다.</summary>
        Vector3d Position { get; set; }

        /// <summary>
        /// 현재 카메라의 위치와 대상 정보를 바탕으로 64비트 뷰 행렬(View Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 행렬을 반환합니다.</returns>
        Matrix4d GetViewMatrix();

        /// <summary>
        /// 현재 카메라의 시야각 및 클리핑 평면 정보를 바탕으로 64비트 투영 행렬(Projection Matrix)을 계산합니다.
        /// </summary>
        /// <returns>64비트 정밀도의 <see cref="Matrix4d"/> 투영 행렬을 반환합니다.</returns>
        Matrix4d GetProjectionMatrix();
    }
}
