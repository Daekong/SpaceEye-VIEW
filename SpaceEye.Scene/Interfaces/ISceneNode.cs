using OpenTK;

namespace SpaceEye.Scene.Interfaces
{
    /// <summary>
    /// 3D 공간에 존재하는 렌더링 가능한 객체의 기본 인터페이스입니다.
    /// </summary>
    internal interface ISceneNode
    {
        /// <summary>
        /// GPU 리소스(VBO, 셰이더 등)를 초기화합니다.
        /// </summary>
        void Initialize();

        /// <summary>
        /// 주어진 카메라 행렬을 바탕으로 자신을 화면에 그립니다.
        /// </summary>
        /// <param name="view">뷰 행렬</param>
        /// <param name="projection">투영 행렬</param>
        void Draw(Matrix4d view, Matrix4d projection);        
    }
}
