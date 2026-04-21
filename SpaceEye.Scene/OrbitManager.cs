using OpenTK;
using SpaceEye.Core.Orbit;
using SpaceEye.Scene.Interfaces;
using SpaceEye.Scene.Orbit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Scene
{
    /// <summary>
    /// SpaceEye 엔진 내의 모든 인공위성 궤도 데이터를 중앙에서 관리하고 통제하는 싱글톤 매니저 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 데이터 관리와 수학적 물리 연산(SGP4)을 전담하며, 그래픽 렌더링은 내부적으로 생성한 <see cref="OrbitSystemNode"/>에 위임합니다.
    /// </remarks>
    internal class OrbitManager
    {
        /// <summary>
        /// <see cref="OrbitManager"/>의 단일 인스턴스에 접근하기 위한 프로퍼티입니다.
        /// </summary>
        public static OrbitManager Instance { get; } = new OrbitManager();

        /// <summary>
        /// Norad ID를 Key로, GPU 배열(SSBO)의 인덱스를 Value로 매핑하는 스레드 안전 딕셔너리입니다.
        /// </summary>
        /// <remarks>
        /// 외부 관리 요소에서 특정 위성을 On/Off 하거나 업데이트할 때, O(1)의 속도로 GPU 버퍼 내의 위치를 찾기 위해 사용됩니다.
        /// </remarks>
        private readonly ConcurrentDictionary<int, int> _noradToIndexMap;

        /// <summary>
        /// GPU로 한 번에 전송할 TLE 파라미터 구조체들의 연속된 리스트입니다.
        /// </summary>
        /// <remarks>
        /// 이 리스트의 인덱스는 <see cref="_noradToIndexMap"/>의 Value와 일치해야 합니다.
        /// 배열 복사 및 GPU 업로드 시 원본 데이터로 활용됩니다.
        /// </remarks>
        private readonly List<OrbitDataForGpu> _orbitDataList;

        /// <summary>
        /// UniverseScene에 삽입되어 실제 Draw Call을 수행할 렌더링 노드입니다.
        /// </summary>
        private readonly OrbitSystemNode _renderNode;

        /// <summary>
        /// <see cref="OrbitManager"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        private OrbitManager()
        {
            _noradToIndexMap = new ConcurrentDictionary<int, int>();
            _orbitDataList = new List<OrbitDataForGpu>();
            _renderNode = new OrbitSystemNode(this);
        }

        /// <summary>
        /// 렌더링 계층인 UniverseScene에 추가할 수 있도록 위성 렌더링 전용 노드를 반환합니다.
        /// </summary>
        /// <returns>위성 궤도를 렌더링하는 <s 
        /// <param name="noradId">제거할 위성의 고유 식별자입니다.</param>
        public void RemoveSatellite(int noradId)
        {
            if (_noradToIndexMap.TryRemove(noradId, out int indexToRemove))
            {
                // 배열 중간이 삭제되면 인덱스가 꼬이므로, 보통 마지막 요소를 삭제된 위치로 덮어씌우는 최적화(Swap and Pop)를 사용합니다.
                int lastIndex = _orbitDataList.Count - 1;

                if (indexToRemove != lastIndex)
                {
                    // 마지막 데이터를 지워진 위치로 이동
                    var lastData = _orbitDataList[lastIndex];
                    _orbitDataList[indexToRemove] = lastData;

                    // 딕셔너리 갱신
                    _noradToIndexMap[lastData.NoradId] = indexToRemove;
                }

                // 리스트의 마지막 요소 제거
                _orbitDataList.RemoveAt(lastIndex);

                // TODO: GPU SSBO 데이터 동기화
            }
        }
    }
}
