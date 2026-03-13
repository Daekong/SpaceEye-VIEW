using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Interfaces
{
    /// <summary>
    /// 우주 공간의 렌더링 환경을 정의하는 인터페이스입니다.
    /// </summary>
    /// <remarks>
    /// 이 인터페이스는 외부 모듈(Renderer 등)에서 실제 구현체인 <c>UniverseScene</c>의 내부 구조를 몰라도
    /// 일괄 렌더링을 요청할 수 있도록 추상화된 기능을 제공합니다.
    /// </remarks>
    public interface IUniverseScene
    {
        /// <summary>
        /// 씬에 등록된 모든 가시 객체(지구, 위성, 궤도 등)를 현재 카메라 시점에 맞춰 렌더링합니다.
        /// </summary>
        /// <param name="view">
        /// 카메라의 위치와 방향을 나타내는 64비트 정밀도 뷰 행렬(View Matrix)입니다.
        /// </param>
        /// <param name="projection">
        /// 화면의 화각(FOV)과 종횡비를 결정하는 64비트 정밀도 투영 행렬(Projection Matrix)입니다.
        /// </param>
        /// <remarks>
        /// 이 메서드가 호출되면 씬 그래프에 포함된 모든 노드들이 
        /// 전달받은 행렬을 기반으로 GPU 파이프라인에 정점을 투사합니다.
        /// </remarks>
        void RenderAll(Matrix4d view, Matrix4d projection);
    }
}
