using OpenTK;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Interfaces;
using SpaceEye.Core.Camera;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Common.Scene
{
    /// <summary>
    /// 우주 공간에 존재하는 모든 렌더링 노드(<see cref="ISceneNode"/>)를 통합 관리하는 싱글톤 씬 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 이 클래스는 전역적으로 단 하나의 인스턴스만 존재하며, 여러 뷰포트(ViewLeft, ViewRight)가 
    /// 동일한 우주 데이터(지구, 위성, 궤도 등)를 공유하여 렌더링할 수 있도록 설계되었습니다.
    /// </remarks>
    internal class UniverseScene : IUniverseScene, IDisposable
    {
        #region # Singleton Instance

        /// <summary>
        /// <see cref="UniverseScene"/>의 유일한 인스턴스를 지연 로딩(Lazy Loading) 방식으로 생성합니다.
        /// </summary>
        /// <remarks>
        /// <see cref="Lazy{T}"/>는 멀티스레드 환경에서 인스턴스 생성을 보장(Thread-Safe)합니다.
        /// </remarks>
        private static readonly Lazy<UniverseScene> _instance =
            new Lazy<UniverseScene>(() => new UniverseScene());

        /// <summary>
        /// <see cref="UniverseScene"/> 클래스의 싱글톤 인스턴스를 가져옵니다.
        /// </summary>
        public static UniverseScene Instance => _instance.Value;

        #endregion

        #region # Fields        
        /// <summary>
        /// 현재 씬에 등록된 모든 렌더링 노드의 목록입니다.
        /// </summary>
        private readonly List<ISceneNode> _nodes = new List<ISceneNode>();

        // 업데이트가 필요한 노드만 따로 모은 리스트 (캐싱)
        private readonly List<ITimeUpdateable> _updateableNodes = new List<ITimeUpdateable>();

        #endregion

        #region # Properties

        /// <summary>와이어 프레임 도시 여부입니다.</summary>
        public bool IsWireframe { get; set; }

        /// <summary>
        ///     시간에 따른 업데이트 여부
        /// </summary>
        public bool IsTimeUpdate { get; set; }

        #endregion

        #region # Constructor

        /// <summary>
        /// <see cref="UniverseScene"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <remarks>
        /// 싱글톤 패턴 유지를 위해 생성자를 <c>private</c>으로 제한합니다.
        /// </remarks>
        private UniverseScene()
        {
            IsWireframe = false;
        }

        #endregion

        #region # IUniverseScene

        /// <summary>
        /// 씬에 등록된 모든 노드를 현재 카메라 시점을 기준으로 일괄 렌더링합니다.
        /// </summary>
        /// <param name="view">카메라의 뷰 행렬(View Matrix)입니다.</param>
        /// <param name="projection">카메라의 투영 행렬(Projection Matrix)입니다.</param>
        /// <remarks>
        /// 이 메서드는 씬에 포함된 각 노드의 <see cref="ISceneNode.Draw(Matrix4d, Matrix4d)"/>를 순차적으로 호출합니다.
        /// </remarks>
        public void RenderAll(Matrix4d view, Matrix4d projection)
        {
            // 렌더링 도중 노드가 동적으로 삭제되는 상황이 발생할 경우 
            // foreach 대신 역순 for문 사용을 권장합니다.
            foreach (var node in _nodes)
            {
                node.Draw(view, projection);
            }
        }

        #endregion

        #region # ITimeUpdateable

        /// <summary>
        /// 씬에 등록된 노드 중 시간 업데이트가 필요한 객체들의 상태를 일괄 갱신합니다.
        /// </summary>
        /// <param name="deltaSeconds">프레임 간 경과 시간(초)입니다.</param>
        /// <param name="ICamera">카메라</param>
        /// <remarks>
        /// <see cref="ITimeUpdateable"/> 인터페이스를 구현한 노드만 선별하여 <c>Update</c>를 호출합니다.
        /// </remarks>
        public void UpdateAll(double deltaSeconds, ICamera camera)
        {           
            // 성능 최적화가 필요할 경우, AddNode 시점에 업데이트 가능 노드만 별도 리스트로 관리할 수 있습니다.
            foreach (var node in _updateableNodes)
                node.Update(deltaSeconds, camera);
                   
        }

        #endregion

        #region # Public Methods

        /// <summary>
        /// 새로운 렌더링 노드를 씬에 추가하고 초기화합니다.
        /// </summary>
        /// <param name="node">추가할 <see cref="ISceneNode"/> 구현체입니다.</param>
        /// <exception cref="ArgumentNullException">추가하려는 노드가 null일 경우 발생합니다.</exception>
        public void AddNode(ISceneNode node)
        {
            if (node == null) 
                throw new ArgumentNullException(nameof(node));

            node.Initialize();
            _nodes.Add(node);

            // [최적화] 노드 추가 시점에 딱 한 번만 타입을 체크하여 캐싱합니다.
            if (node is ITimeUpdateable updateable)            
                _updateableNodes.Add(updateable);           
        }

        /// <summary>
        /// 씬에 등록된 모든 노드를 제거하고, 각 노드가 보유한 GPU 리소스를 즉시 해제합니다.
        /// </summary>
        public void Clear()
        {
            foreach (IDisposable node in _nodes)
            {
               node.Dispose();
            }
            _nodes.Clear();
        }

        #endregion

        #region # IDisposable Support

        /// <summary>
        /// <see cref="UniverseScene"/> 인스턴스와 등록된 모든 렌더링 노드의 리소스를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            Clear();
        }

        #endregion
    }
}
