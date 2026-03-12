using OpenTK;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Scene
{
    /// <summary>
    /// 우주 공간에 존재하는 모든 렌더링 노드(<see cref="ISceneNode"/>)를 통합 관리하는 씬 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 이 클래스는 지구, 달, 위성 등 개별적인 노드들을 리스트로 관리하며, 
    /// 렌더러로부터 전달받은 카메라 시점을 모든 노드에 전파하여 일괄 렌더링을 수행합니다.
    /// </remarks>
    internal class UniverseScene : IDisposable
    {
        #region # Feilds
        /// <summary>
        /// 현재 씬에 등록된 모든 렌더링 노드의 목록입니다.
        /// </summary>
        private readonly List<ISceneNode> _nodes = new List<ISceneNode>();

        #endregion

        #region # Constructor

        /// <summary>
        /// <see cref="UniverseScene"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        public UniverseScene()
        {
        }

        #endregion

        #region # Public Method

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
        }

        /// <summary>
        /// 씬에 등록된 모든 노드를 현재 카메라 시점을 기준으로 렌더링합니다.
        /// </summary>
        /// <param name="view">카메라의 뷰 행렬(View Matrix)입니다.</param>
        /// <param name="projection">카메라의 투영 행렬(Projection Matrix)입니다.</param>
        /// <remarks>
        /// 이 메서드는 각 노드의 <see cref="ISceneNode.Draw(Matrix4, Matrix4)"/>를 순차적으로 호출합니다.
        /// </remarks>
        public void RenderAll(Matrix4d view, Matrix4d projection)
        {
            // 리스트 수정 중 순회 오류를 방지하기 위해 필요한 경우 for문이나 복사본 사용을 고려할 수 있습니다.
            foreach (var node in _nodes)
            {
                node.Draw(view, projection);
            }
        }

        /// <summary>
        /// 씬에 등록된 모든 노드를 제거하고 관련 GPU 리소스를 해제합니다.
        /// </summary>
        public void Clear()
        {
            foreach (var node in _nodes)
            {
                node.Dispose();
            }
            _nodes.Clear();
        }

        #endregion

        #region # IDisposable Method

        /// <summary>
        /// <see cref="UniverseScene"/>에서 사용 중인 모든 리소스를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            Clear();
        }

        #endregion
    }
}
