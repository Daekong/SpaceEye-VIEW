using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Extensions;
using SpaceEye.Common.Interfaces;
using SpaceEye.Core.Camera;
using SpaceEye.Core.Common;
using SpaceEye.Core.Helpers;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace SpaceEye.Scene.Celestial
{
    /// <summary>
    /// 쿼드트리(Quadtree) 기반의 Chunked LOD를 관리하고 와이어프레임(Wireframe)으로 시각화하는 지구 렌더링 노드 클래스입니다.
    /// </summary>
    /// <remarks>
    /// 이 클래스는 지표면을 6개의 루트 청크로 분할한 뒤, 카메라와의 거리에 따라 동적으로 
    /// 하위 청크를 생성(Split)하거나 병합(Merge)하여 최적화된 렌더링 성능을 제공합니다.    
    /// </remarks>
    internal class EarthNodeLod : ISceneNode, ITimeUpdateable, IDisposable
    {
        #region # Fields

        /// <summary>
        /// GPU에 저장된 정점 속성 상태를 보관하는 Vertex Array Object (VAO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vao;

        /// <summary>
        /// 실제 정점 데이터(위치 좌표)를 GPU 메모리에 보관하는 Vertex Buffer Object (VBO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _vbo;

        /// <summary>
        /// 정점들의 연결 순서를 정의하는 인덱스 데이터를 GPU 메모리에 보관하는 Element Buffer Object (EBO)의 OpenGL 식별자입니다.
        /// </summary>
        private int _ebo;

        /// <summary>
        /// 정점(VS), 제어(TCS), 평가(TES), 단편(FS) 셰이더가 링크된 최종 셰이더 프로그램의 OpenGL 식별자입니다.
        /// </summary>
        private int _shader;

        /// <summary>자원 초기화 메서드(<see cref="Initialize"/>)가 성공적으로 완료되었는지를 나타내는 상태 플래그입니다.</summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 쿼드트리의 최상위 6개 면(정육면체를 구형으로 부풀린 형태)을 담는 리스트입니다.
        /// </summary>
        private List<TerrainChunk> _rootChunks;

        /// <summary>
        /// 이번 프레임에 최종적으로 화면에 그려질 최하위(Leaf) 청크들의 목록입니다.
        /// 매 프레임마다 <see cref="Update"/> 단계에서 갱신됩니다.
        /// </summary>
        private List<TerrainChunk> _renderQueue;

        #endregion

        #region # Properties

        #endregion

        #region # Constructor

        #endregion

        #region # ITimeUpdateable

        /// <summary>
        /// 매 프레임 카메라 위치와 시야를 기준으로 쿼드트리를 순회하며 청크의 분할(Split) 및 선별(Culling)을 수행합니다.
        /// </summary>
        /// <param name="deltaSeconds">이전 프레임부터 경과된 시간(초)입니다.</param>       
        /// <param name="ICamera">카메라 인터페이스</param>
        public void Update(double deltaSeconds, ICamera camera)
        {
            if (!_isInitialized) return;

            _renderQueue.Clear();

            // 카메라의 뷰-투영 행렬을 곱하여 절두체 추출기를 생성합니다.
            Matrix4d vpMatrix = camera.GetProjectionMatrix() * camera.GetViewMatrix();
            FrustumCuller frustum = new FrustumCuller(vpMatrix);

            foreach (var chunk in _rootChunks)
            {
                chunk.UpdateLOD(camera, frustum, _renderQueue);
            }
        }

        #endregion

        #region # ISceneNode

        /// <summary>
        /// 셰이더를 컴파일하고 GPU에 정점 데이터를 할당하여 렌더링을 준비합니다.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            // 1. 디버깅용 심플 셰이더 생성
            _shader = CreateWireframeShader();

            // 2. OpenGL 버퍼 생성
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            // 3. 최상위 루트 청크 6개 생성 (Cube Sphere)
            _rootChunks = new List<TerrainChunk>();
            _renderQueue = new List<TerrainChunk>();
            CreateRootChunks();

            _isInitialized = true;
        }

        /// <summary>
        /// 화면에 지구 노드를 그립니다. 매 프레임마다 호출됩니다.
        /// </summary>
        /// <param name="view">카메라의 위치와 방향을 나타내는 뷰 행렬 (64비트)</param>
        /// <param name="projection">카메라의 원근감을 나타내는 투영 행렬 (64비트)</param>
        public void Draw(Matrix4d view, Matrix4d projection)
        {
            if(!_isInitialized || _renderQueue.Count == 0) return;

            GL.UseProgram(_shader);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            Matrix4d model = Matrix4d.Identity;
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "projection"), false, ref projection);

            // 동적 버퍼 빌드: 화면에 그릴 청크들의 정점을 하나의 배열로 모읍니다.
            List<double> vertices = new List<double>();
            List<uint> indices = new List<uint>();
            uint currentIndex = 0;

            foreach (var chunk in _renderQueue)
            {
                vertices.Add(chunk.V0.X); vertices.Add(chunk.V0.Y); vertices.Add(chunk.V0.Z);
                vertices.Add(chunk.V1.X); vertices.Add(chunk.V1.Y); vertices.Add(chunk.V1.Z);
                vertices.Add(chunk.V2.X); vertices.Add(chunk.V2.Y); vertices.Add(chunk.V2.Z);
                vertices.Add(chunk.V3.X); vertices.Add(chunk.V3.Y); vertices.Add(chunk.V3.Z);

                indices.Add(currentIndex + 0); indices.Add(currentIndex + 1); indices.Add(currentIndex + 2);
                indices.Add(currentIndex + 0); indices.Add(currentIndex + 2); indices.Add(currentIndex + 3);
                currentIndex += 4;
            }

            // GPU 메모리 업데이트
            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(double), vertices.ToArray(), BufferUsageHint.StreamDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StreamDraw);

            GL.VertexAttribLPointer(0, 3, VertexAttribDoubleType.Double, 3 * sizeof(double), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);

            // 와이어프레임 모드 활성화 및 렌더링
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        #endregion

        #region # Private Method

        #region # Private Methods

        /// <summary>
        /// 단위 정육면체의 6개 면을 구형(Sphere)으로 투영하여 최상위 수준의 쿼드트리 노드들을 초기 생성합니다.
        /// </summary>
        private void CreateRootChunks()
        {
            Vector3d[] corners = {
                new Vector3d(-1, 1, 1), new Vector3d(1, 1, 1), new Vector3d(1, -1, 1), new Vector3d(-1, -1, 1), // Front
                new Vector3d(1, 1, -1), new Vector3d(-1, 1, -1), new Vector3d(-1, -1, -1), new Vector3d(1, -1, -1), // Back
                new Vector3d(-1, 1, -1), new Vector3d(-1, 1, 1), new Vector3d(-1, -1, 1), new Vector3d(-1, -1, -1), // Left
                new Vector3d(1, 1, 1), new Vector3d(1, 1, -1), new Vector3d(1, -1, -1), new Vector3d(1, -1, 1), // Right
                new Vector3d(-1, 1, -1), new Vector3d(1, 1, -1), new Vector3d(1, 1, 1), new Vector3d(-1, 1, 1), // Top
                new Vector3d(-1, -1, 1), new Vector3d(1, -1, 1), new Vector3d(1, -1, -1), new Vector3d(-1, -1, -1)  // Bottom
            };

            for (int i = 0; i < 6; i++)
            {
                _rootChunks.Add(new TerrainChunk(
                    0, Earth.EarthRadius,
                    Vector3d.Normalize(corners[i * 4 + 0]) * Earth.EarthRadius,
                    Vector3d.Normalize(corners[i * 4 + 1]) * Earth.EarthRadius,
                    Vector3d.Normalize(corners[i * 4 + 2]) * Earth.EarthRadius,
                    Vector3d.Normalize(corners[i * 4 + 3]) * Earth.EarthRadius
                ));
            }
        }

        /// <summary>
        /// 와이어프레임을 렌더링하기 위한 단순한 기본 색상 셰이더를 생성하고 컴파일합니다.
        /// </summary>
        /// <returns>성공적으로 링크된 셰이더 프로그램의 정수 ID를 반환합니다.</returns>
        private int CreateWireframeShader()
        {
            string vSrc = "#version 410 core\n" +
                          "layout(location = 0) in dvec3 aPos;\n" +
                          "uniform dmat4 model, view, projection;\n" +
                          "void main() {\n" +
                          "    gl_Position = vec4(projection * view * model * dvec4(aPos, 1.0lf));\n" +
                          "}\n";

            string fSrc = "#version 410 core\n" +
                          "out vec4 FragColor;\n" +
                          "void main() {\n" +
                          "    FragColor = vec4(0.0, 1.0, 0.0, 1.0);\n" +
                          "}\n";

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vSrc);
            GL.CompileShader(vs);

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fSrc);
            GL.CompileShader(fs);

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;
        }

        #endregion

        #endregion

        #region # IDisposable

        /// <summary>
        /// 할당된 모든 OpenGL 자원(VBO, VAO, Shader 등)을 메모리에서 해제합니다.
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);
                GL.DeleteVertexArray(_vao);
                GL.DeleteProgram(_shader);
                _isInitialized = false;
            }
        }

        public void Update(double deltaSeconds, System.Windows.Media.Media3D.Camera camera)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    /// <summary>
    /// 쿼드트리의 개별 노드(Chunk) 데이터를 정의하고, 카메라 거리에 따른 동적 분할(Split) 로직을 처리하는 내부 클래스입니다.
    /// </summary>
    internal class TerrainChunk
    {
        /// <summary>현재 청크의 LOD 깊이 수준입니다. 0(Root)부터 시작하여 분할될 때마다 1씩 증가합니다.</summary>
        public int Level { get; }

        /// <summary>청크의 테두리를 구성하는 4개의 모서리 절대 정점 64비트 좌표입니다.</summary>
        public Vector3d V0, V1, V2, V3;

        /// <summary>카메라와의 거리 계산 기준이 되는 청크의 중앙 좌표입니다.</summary>
        public Vector3d Center;

        /// <summary>분할 조건을 만족했을 때 생성되는 4개의 하위 쿼드트리 노드(자식 청크) 배열입니다.</summary>
        public TerrainChunk[] Children;

        /// <summary>분할된 모서리의 중간점을 구의 표면으로 밀어낼 때 사용하는 행성의 기준 반지름입니다.</summary>
        private readonly double _radius;

        /// <summary>메모리 오버플로우 및 무한 재귀 분할을 방지하기 위한 최대 깊이(Depth) 제한 상수입니다.</summary>
        private const int MaxLevel = 7;

        /// <summary>분할 민감도를 결정하는 계수입니다. 이 값이 클수록 카메라에서 더 먼 거리에서도 분할이 발생합니다.</summary>
        private const double SplitMultiplier = 4;

        /// <summary>
        /// 새로운 지형 청크 인스턴스를 초기화하고 중심점(Center)을 자동으로 연산합니다.
        /// </summary>
        /// <param name="level">현재 노드의 쿼드트리 깊이(LOD 레벨)입니다.</param>
        /// <param name="radius">투영 곡률 계산을 위한 행성의 반지름(Km)입니다.</param>
        /// <param name="v0">청크의 좌하단 모서리 정점 좌표</param>
        /// <param name="v1">청크의 우하단 모서리 정점 좌표</param>
        /// <param name="v2">청크의 우상단 모서리 정점 좌표</param>
        /// <param name="v3">청크의 좌상단 모서리 정점 좌표</param>
        public TerrainChunk(int level, double radius, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d v3)
        {
            Level = level;
            _radius = radius;
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;

            // 중심점 연산: 4개 꼭짓점의 평균을 낸 후 구형으로 둥글게 보정(Normalize * Radius)합니다.
            Center = Vector3d.Normalize((v0 + v1 + v2 + v3) / 4.0) * _radius;
        }

        /// <summary>
        /// 카메라와의 거리를 측정하여 현재 청크를 4개로 쪼갤지(Split), 아니면 그대로 유지할지 결정합니다.
        /// </summary>
        /// <param name="cameraPos">카메라의 현재 64비트 월드 좌표입니다.</param>
        /// <param name="renderQueue">렌더링할 대상을 담는 리스트입니다. 더 이상 분할되지 않는 최하단 노드(Leaf)만 여기에 추가됩니다.</param>
        public void UpdateLOD(ICamera camera, FrustumCuller frustum, List<TerrainChunk> renderQueue)
        {
            Vector3d cameraPos = camera.Position;
            double cameraDist = cameraPos.Length;
            double boundingRadius = Vector3d.Distance(Center, V0);
            double sseFactor = camera.ScreenHeight / (2.0 * Math.Tan(camera.Fov.ToRadian() / 2.0));

            // 1. [Frustum Culling] 절두체 시야에서 벗어난 청크 버림
            //if (!frustum.IntersectsSphere(Center, boundingRadius))
            //    return;

            // ⭐ 2. [Horizon Culling] 우주 스케일에 맞춘 동적 각도 기반 뒷면 선별
            // 지구 중심(0,0,0)에서 청크와 카메라를 향하는 각각의 방향 벡터를 구합니다.           
            // 카메라가 우주 공간(지구 밖)에 있을 때만 지평선 선별 작동
            if (cameraDist > _radius)
            {
                // 1. 지구 중심에서 실제 지평선이 형성되는 각도 (Alpha)
                double alpha = Math.Acos(_radius / cameraDist);

                // 2. 현재 청크의 크기(Bounding Radius)가 덮고 있는 여유 각도 (Beta)
                // (루트 청크처럼 덩치가 클 때 Asin 범위를 벗어나지 않도록 Max/Min 클램핑)
                double sinBeta = Math.Max(-1.0, Math.Min(1.0, boundingRadius / _radius));
                double beta = Math.Asin(sinBeta);

                // 3. 지평선 한계치 절대 각도 (이 각도를 넘어가면 100% 안 보임)
                double thresholdAngle = alpha + beta;

                // 4. 카메라와 현재 청크 중심이 이루는 실제 각도 (Gamma)
                Vector3d chunkNormal = Vector3d.Normalize(Center);
                Vector3d cameraDir = Vector3d.Normalize(cameraPos);

                double dot = Math.Max(-1.0, Math.Min(1.0, Vector3d.Dot(chunkNormal, cameraDir)));
                double gamma = Math.Acos(dot);

                // 5. 청크의 각도가 한계치를 넘어 지평선 뒤로 숨었다면 즉각 소멸!
                if (gamma > thresholdAngle)
                {
                    return;
                }
            }

            // 3. 256x256 텍스처 타일 기반의 분할(Split) 조건 검사
            // 찌그러짐에 취약한 밑변(V0-V1) 대신, 청크를 완벽히 감싸는 구의 '지름'을 사용합니다!
            // boundingRadius는 메서드 최상단 1번에서 이미 구해둔 (Center와 V0 사이의 거리) 값입니다.
            double chunkDiameter = boundingRadius * 2.0;

            // 카메라와의 최단 거리 계산 (5개 포인트 중 최소값)
            double d0 = Vector3d.Distance(cameraPos, V0);
            double d1 = Vector3d.Distance(cameraPos, V1);
            double d2 = Vector3d.Distance(cameraPos, V2);
            double d3 = Vector3d.Distance(cameraPos, V3);
            double dc = Vector3d.Distance(cameraPos, Center);
            double closestDistance = Math.Max(Math.Min(dc, Math.Min(Math.Min(d0, d1), Math.Min(d2, d3))), 1.0);

            // ⭐ [핵심 공식] edgeLength 대신 chunkDiameter를 사용하여 화면 픽셀 크기를 구합니다.
            double chunkScreenSizePixels = (chunkDiameter / closestDistance) * sseFactor;

            // 텍스처 해상도 기준 임계값
            double tileResolutionThreshold = 256.0 * 0.85;

            // 화면 차지 픽셀이 텍스처 한계 해상도를 넘어서려 하면 쪼갭니다!
            if (Level < 2 || (chunkScreenSizePixels > tileResolutionThreshold && Level < MaxLevel))
            {
                if (Children == null) Split();

                foreach (var child in Children)
                {
                    child.UpdateLOD(camera, frustum, renderQueue);
                }
            }
            else
            {
                Children = null;
                renderQueue.Add(this);
            }
        }

        /// <summary>
        /// 현재 사각형 청크를 4개의 더 작은 쿼드트리 하위 청크로 등분할합니다.
        /// </summary>
        /// <remarks>
        /// 단순히 평면을 4등분 하는 것이 아니라, 새로 생성되는 모서리들의 중간 지점(Midpoints)을 
        /// <see cref="Vector3d.Normalize"/> 연산을 통해 구의 표면으로 다시 밀어내어 완벽한 곡면 형상을 유지합니다.
        /// </remarks>
        private void Split()
        {
            Vector3d m01 = Vector3d.Normalize((V0 + V1) / 2.0) * _radius;
            Vector3d m12 = Vector3d.Normalize((V1 + V2) / 2.0) * _radius;
            Vector3d m23 = Vector3d.Normalize((V2 + V3) / 2.0) * _radius;
            Vector3d m30 = Vector3d.Normalize((V3 + V0) / 2.0) * _radius;

            Children = new TerrainChunk[4];
            Children[0] = new TerrainChunk(Level + 1, _radius, V0, m01, Center, m30); // 0사분면 (좌하단)
            Children[1] = new TerrainChunk(Level + 1, _radius, m01, V1, m12, Center); // 1사분면 (우하단)
            Children[2] = new TerrainChunk(Level + 1, _radius, Center, m12, V2, m23); // 2사분면 (우상단)
            Children[3] = new TerrainChunk(Level + 1, _radius, m30, Center, m23, V3); // 3사분면 (좌상단)
        }
    }
}
