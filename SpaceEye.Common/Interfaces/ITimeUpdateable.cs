using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Interfaces
{
    /// <summary>
    /// 시간에 따라 상태가 변화하는 객체(지구 자전, 위성 이동, 애니메이션 등)를 위한 인터페이스입니다.
    /// </summary>
    /// <remarks>
    /// 모든 노드가 업데이트될 필요는 없으므로, 동적 움직임이 필요한 노드에만 선택적으로 구현합니다.
    /// </remarks>
    public interface ITimeUpdateable
    {
        /// <summary>
        /// 경과 시간(Delta Time)을 기반으로 객체의 내부 상태(회전각, 좌표 등)를 업데이트합니다.
        /// </summary>
        /// <param name="deltaSeconds">이전 프레임 이후 현재 프레임까지 경과된 초 단위 시간입니다.</param>
        /// <remarks>
        /// 프레임 독립적인 물리 연산을 위해 반드시 이 <paramref name="deltaSeconds"/>를 계산에 활용해야 합니다.
        /// </remarks>
        void Update(double deltaSeconds);
    }
}
