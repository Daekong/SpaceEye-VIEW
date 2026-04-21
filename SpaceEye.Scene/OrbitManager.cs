using OpenTK;
using SpaceEye.Common.Scene;
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
        #region # Singleton Instance

        /// <summary>
        /// <see cref="OrbitManager"/>의 유일한 인스턴스를 지연 로딩(Lazy Loading) 방식으로 생성합니다.
        /// </summary>
        /// <remarks>
        /// <see cref="Lazy{T}"/>는 멀티스레드 환경에서 인스턴스 생성을 보장(Thread-Safe)합니다.
        /// </remarks>
        private static readonly Lazy<OrbitManager> _instance =
            new Lazy<OrbitManager>(() => new OrbitManager());

        /// <summary>
        /// <see cref="OrbitManager"/> 클래스의 싱글톤 인스턴스를 가져옵니다.
        /// </summary>
        public static OrbitManager Instance => _instance.Value;

        #endregion
      
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
        /// 변경 여부
        /// </summary>
        private bool _isDirty = false;

        #region # Properties

        /// <summary>
        /// 현재 GPU에 등록되어 활성화된 위성의 총 개수를 가져옵니다.
        /// </summary>
        public int ActiveSatelliteCount => _orbitDataList.Count;

        /// <summary>
        /// TLE 원기(Epoch)로부터 경과된 시간입니다. 사용자가 UI에서 시간을 조작할 때 이 값을 업데이트합니다.
        /// </summary>
        public double CurrentTimeSinceEpoch { get; set; } = 0.0;

        #endregion

        /// <summary>
        /// <see cref="OrbitManager"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        private OrbitManager()
        {
            _noradToIndexMap = new ConcurrentDictionary<int, int>();
            _orbitDataList = new List<OrbitDataForGpu>();
            _renderNode = new OrbitSystemNode(this);
        }

        #region # Public Method

        /// <summary>
        /// 위성 TLE 데이터에 변경 사항이 있을 경우, 이를 감지하여 GPU의 SSBO(Shader Storage Buffer Object)로 동기화합니다.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>이 메서드는 내부적으로 Dirty Flag 패턴(<c>_isDirty</c>)을 사용하여 매 프레임 발생하는 불필요한 GPU 메모리 복사(Overhead)를 완벽하게 차단합니다.</description></item>
        /// <item><description>데이터 추가, 삭제, 또는 갱신이 발생하여 플래그가 활성화되었을 때만 내부 리스트를 배열로 변환한 후, <see cref="OrbitSystemNode.SyncTleDataToGpu"/>를 호출하여 VRAM으로 전송합니다.</description></item>
        /// <item><description>Zero-Copy 렌더링 파이프라인의 핵심 진입점으로, 렌더 루프(Render Loop) 내부에서 매 프레임 호출하더라도 변경 사항이 없으면 즉시 반환되므로 성능 저하가 없습니다.</description></item>
        /// </list>
        /// </remarks>
        public void UpdateGpuBuffers()
        {
            if (_isDirty)
            {
                // 1. 내부 TLE 리스트를 연속된 메모리 배열로 변환
                var dataArray = _orbitDataList.ToArray();

                // 2. 렌더링 노드에 배열 원본과 활성화된 개수를 넘겨 GPU로 초고속 부분 전송(BufferSubData)
                _renderNode.SyncOrbitDataToGpu(dataArray, dataArray.Length);

                // 3. 전송 완료 후 플래그 초기화
                _isDirty = false;
            }
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

        #endregion

    }
}
