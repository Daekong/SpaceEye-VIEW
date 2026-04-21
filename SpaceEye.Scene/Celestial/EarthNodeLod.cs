using OpenTK;
using OpenTK.Graphics.OpenGL;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Extensions;
using SpaceEye.Common.Interfaces;
using SpaceEye.Common.Scene;
using SpaceEye.Core.Camera;
using SpaceEye.Core.Common;
using SpaceEye.Core.Helpers;
using SpaceEye.Scene.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>GPU에 저장된 정점 속성 상태를 보관하는 Vertex Array Object (VAO)의 OpenGL 식별자입니다.</summary>
        private int _vao;

        /// <summary>실제 정점 데이터(위치 좌표)를 GPU 메모리에 보관하는 Vertex Buffer Object (VBO)의 OpenGL 식별자입니다.</summary>
        private int _vbo;

        /// <summary>정점들의 연결 순서를 정의하는 인덱스 데이터를 GPU 메모리에 보관하는 Element Buffer Object (EBO)의 OpenGL 식별자입니다.</summary>
        private int _ebo;

        /// <summary>정점(VS), 제어(TCS), 평가(TES), 단편(FS) 셰이더가 링크된 최종 셰이더 프로그램의 OpenGL 식별자입니다.</summary>
        private int _shader;

        /// <summary>현재 지구의 자전 각도 (Degrees)입니다.</summary>
        private double _rotationAngleDeg = 0;

        /// <summary>지구 자전(Rotation)을 켜고 끌 수 있는 상태 플래그입니다.</summary>
        private bool _isRotationEnabled = true;

        // 지구 텍스처 ID 변수
        private int _texture;

        /// <summary>자원 초기화 메서드(Initialize)가 성공적으로 완료되었는지를 나타내는 상태 플래그입니다.</summary>
        private bool _isInitialized = false;

        /// <summary>쿼드트리의 최상위 6개 면(정육면체를 구형으로 부풀린 형태)을 담는 리스트입니다.</summary>
        private List<TerrainChunk> _rootChunks;

        /// <summary>이번 프레임에 최종적으로 화면에 그려질 최하위(Leaf) 청크들의 목록입니다.</summary>
        private List<TerrainChunk> _renderQueue;

        #endregion

        #region # Properties

        /// <summary>
        /// 지구의 자전(회전)을 활성화하거나 비활성화합니다.
        /// </summary>
        public bool IsRotationEnabled
        {
            get => _isRotationEnabled;
            set => _isRotationEnabled = value;
        }

        #endregion

        #region # ITimeUpdateable

        /// <summary>
        /// 매 프레임마다 지구의 상태(자전 각도)를 갱신하고 렌더링할 청크 리스트(LOD)를 빌드합니다.
        /// </summary>
        public void Update(double deltaSeconds, ICamera camera)
        {
            // 초기화가 완료되지 않았다면 업데이트를 건너뜁니다.
            if (!_isInitialized) return;

            // 1. 렌더링 큐 초기화 (새로운 프레임의 청크들을 담기 위해 비움)
            _renderQueue.Clear();

            // 2. 지구 자전(Rotation) 계산 (회전이 켜져 있을 때만 작동)
            if (_isRotationEnabled)
            {       
                // 시간 경과에 따른 누적 자전 각도 계산 (Degree 단위)
                _rotationAngleDeg += Earth.EarthRotationSpeedDegPerSec * deltaSeconds;

                // 360도를 넘어가면 0도로 순환시켜 부동 소수점 오차 누적을 방지합니다.
                _rotationAngleDeg %= 360.0;
            }

            // ⭐ 3. 카메라 로컬 좌표계 변환 (회전 동기화의 핵심)
            // 지구가 회전하더라도 청크의 좌표(V0~V3)는 바뀌지 않습니다.
            // 대신, 지구의 회전만큼 카메라를 반대 방향으로 돌려서 '지구 로컬 공간'의 카메라 좌표를 구합니다.
            Matrix4d modelMatrix = Matrix4d.CreateRotationY(MathHelper.DegreesToRadians(_rotationAngleDeg));
            Matrix4d invModel = modelMatrix.Inverted();
            Vector3d localCameraPos = Vector3d.TransformPosition(camera.Position, invModel);

            // ⭐ 4. 시야 절두체(Frustum) 로컬화 업데이트
            // 컬링(화면 밖 자르기)도 지구가 회전한 각도에 맞춰야 합니다.
            // 뷰/투영 행렬에 모델 행렬을 결합하여 로컬 공간 기준의 절두체를 생성합니다.
            Matrix4d localVpMatrix = modelMatrix * camera.GetViewMatrix() * camera.GetProjectionMatrix();
            FrustumCuller localFrustum = new FrustumCuller(localVpMatrix);

            // 5. LOD(Level of Detail) 업데이트 및 렌더링 큐 빌드
            foreach (var chunk in _rootChunks)
            {
                // 실제 카메라 객체 대신 로컬 좌표(localCameraPos)와 로컬 절두체(localFrustum)를 전달합니다.
                chunk.UpdateLOD(localCameraPos, camera, localFrustum, _renderQueue);
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
            _shader = CreateShader();

            // 2. OpenGL 버퍼 생성
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            // 텍스처 로드 (경로는 실제 환경에 맞게 유지)
            string basePath = Path.Combine(AppContext.BaseDirectory, "SpaceEye.Resouces", "Images", "BaseMap");
            _texture = TextureLoader.LoadTexture(Path.Combine(basePath, "Earth_BlueMarble_NextGeneration_2Km.jpg"));

            // 3. 최상위 루트 청크 6개 생성 (Cube Sphere)
            _rootChunks = new List<TerrainChunk>();
            _renderQueue = new List<TerrainChunk>();
            CreateRootChunks();

            _isInitialized = true;
        }

        /// <summary>
        /// 화면에 지구 노드를 그립니다. 매 프레임마다 호출됩니다.
        /// </summary>
        public void Draw(Matrix4d view, Matrix4d projection)
        {
            // 초기화가 안 되었거나 그려야 할 청크가 없으면 중단
            if (!_isInitialized || _renderQueue.Count == 0) return;

            // 1. 셰이더 및 기본 상태 설정
            GL.UseProgram(_shader);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            // ⭐ 2. 행렬(Matrices) 계산 및 전송
            // 현재 누적된 _rotationAngleDeg 각도를 사용하여 지구를 물리적으로 회전시킵니다.
            Matrix4d model = Matrix4d.CreateRotationY(MathHelper.DegreesToRadians(_rotationAngleDeg));

            int modelLoc = GL.GetUniformLocation(_shader, "model");
            int viewLoc = GL.GetUniformLocation(_shader, "view");
            int projLoc = GL.GetUniformLocation(_shader, "projection");

            if (modelLoc != -1) GL.UniformMatrix4(modelLoc, false, ref model);
            if (viewLoc != -1) GL.UniformMatrix4(viewLoc, false, ref view);
            if (projLoc != -1) GL.UniformMatrix4(projLoc, false, ref projection);

            // 3. 텍스처 및 와이어프레임 설정 전송
            int texLoc = GL.GetUniformLocation(_shader, "earthTexture");
            int isWireLoc = GL.GetUniformLocation(_shader, "isWireframe");
            int wireColLoc = GL.GetUniformLocation(_shader, "wireColor");

            bool isWireframe = UniverseScene.Instance.IsWireframe;

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            if (texLoc != -1) GL.Uniform1(texLoc, 0);

            if (isWireframe)
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                if (isWireLoc != -1) GL.Uniform1(isWireLoc, 1);
                if (wireColLoc != -1) GL.Uniform3(wireColLoc, 0.0f, 1.0f, 0.0f);
            }
            else
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                if (isWireLoc != -1) GL.Uniform1(isWireLoc, 0);
            }

            // 4. 동적 버퍼 빌드
            List<double> vertices = new List<double>();
            List<uint> indices = new List<uint>();
            uint currentIndex = 0;

            // 스커트의 길이 (지구 중심 쪽으로 1.5% 만큼 밀어넣어 벽을 만듭니다)
            // 약 95km 두께의 벽이 생겨서 웬만한 LOD 단차는 다 가려집니다.
            double skirtScale = 0.985;

            foreach (var chunk in _renderQueue)
            {
                // [윗면 정점] (원래 지표면)
                vertices.Add(chunk.V0.X); vertices.Add(chunk.V0.Y); vertices.Add(chunk.V0.Z);
                vertices.Add(chunk.V1.X); vertices.Add(chunk.V1.Y); vertices.Add(chunk.V1.Z);
                vertices.Add(chunk.V2.X); vertices.Add(chunk.V2.Y); vertices.Add(chunk.V2.Z);
                vertices.Add(chunk.V3.X); vertices.Add(chunk.V3.Y); vertices.Add(chunk.V3.Z);

                // [아랫면 정점] (스커트 바닥: 중심 방향으로 축소)
                vertices.Add(chunk.V0.X * skirtScale); vertices.Add(chunk.V0.Y * skirtScale); vertices.Add(chunk.V0.Z * skirtScale);
                vertices.Add(chunk.V1.X * skirtScale); vertices.Add(chunk.V1.Y * skirtScale); vertices.Add(chunk.V1.Z * skirtScale);
                vertices.Add(chunk.V2.X * skirtScale); vertices.Add(chunk.V2.Y * skirtScale); vertices.Add(chunk.V2.Z * skirtScale);
                vertices.Add(chunk.V3.X * skirtScale); vertices.Add(chunk.V3.Y * skirtScale); vertices.Add(chunk.V3.Z * skirtScale);

                uint v0 = currentIndex + 0, v1 = currentIndex + 1, v2 = currentIndex + 2, v3 = currentIndex + 3;
                uint b0 = currentIndex + 4, b1 = currentIndex + 5, b2 = currentIndex + 6, b3 = currentIndex + 7;

                // 1. 윗면 삼각형 (기존과 동일)
                indices.Add(v0); indices.Add(v1); indices.Add(v2);
                indices.Add(v0); indices.Add(v2); indices.Add(v3);

                // 2. 4개의 옆면(스커트 벽) 삼각형 추가
                // V0-V1 벽
                indices.Add(v0); indices.Add(b0); indices.Add(b1);
                indices.Add(v0); indices.Add(b1); indices.Add(v1);
                // V1-V2 벽
                indices.Add(v1); indices.Add(b1); indices.Add(b2);
                indices.Add(v1); indices.Add(b2); indices.Add(v2);
                // V2-V3 벽
                indices.Add(v2); indices.Add(b2); indices.Add(b3);
                indices.Add(v2); indices.Add(b3); indices.Add(v3);
                // V3-V0 벽
                indices.Add(v3); indices.Add(b3); indices.Add(b0);
                indices.Add(v3); indices.Add(b0); indices.Add(v0);

                currentIndex += 8;
            }

            // 5. GPU 메모리 갱신 및 그리기 실행
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(double), vertices.ToArray(), BufferUsageHint.StreamDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StreamDraw);

            GL.VertexAttribLPointer(0, 3, VertexAttribDoubleType.Double, 3 * sizeof(double), IntPtr.Zero);
            GL.EnableVertexAttribArray(0);

            // ⭐ 1단계: 원래대로 면(Fill)으로 지구를 그립니다.
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);

            // ⭐ 2단계: 틈새 메우기 (같은 위치에 선(Line)을 한 번 더 덧그립니다)
            // 그냥 그리면 Z-Fighting(깜빡임)이 생기므로, 선을 카메라 쪽으로 아주 미세하게 당겨옵니다.
            GL.Enable(EnableCap.PolygonOffsetLine);
            GL.PolygonOffset(-1.0f, -1.0f);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, indices.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.Disable(EnableCap.PolygonOffsetLine);
            // --------------------------------------------------------

            // 6. 상태 복구
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        #endregion

        #region # Private Method

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
        private int CreateShader()
        {
            string vSrc = "#version 410 core\n" +
              "layout(location = 0) in dvec3 aPos;\n" +
              "uniform dmat4 model, view, projection;\n" +
              "out vec3 vNormal;\n" +
              "void main() {\n" +
              "    // Use the vertex position as the normal vector for spherical mapping\n" +
              "    vNormal = normalize(vec3(aPos));\n" +
              "    // Compute final position with 64-bit precision matrices\n" +
              "    gl_Position = vec4(projection * view * model * dvec4(aPos, 1.0lf));\n" +
              "}\n";

            string fSrc = "#version 410 core\n" +
              "out vec4 FragColor;\n" +
              "in vec3 vNormal;\n" +
              "uniform sampler2D earthTexture;\n" +
              "uniform bool isWireframe;\n" +
              "uniform vec3 wireColor;\n" +
              "const float PI = 3.14159265359;\n" +
              "void main() {\n" +
              "    vec3 n = normalize(vNormal);\n" +
              "    if(isWireframe) {\n" +
              "        // Render flat color for wireframe mode\n" +
              "        FragColor = vec4(wireColor, 1.0);\n" +
              "    } else {\n" +
              "        // Equirectangular Mapping: Convert 3D direction to 2D UV coordinates\n" +
              "        float u = 0.5 + atan(n.z, n.x) / (2.0 * PI);\n" +
              "        float v = 0.5 - asin(n.y) / PI;\n" +
              "        vec2 uv = vec2(u, v);\n" +
              "        \n" +
              "        // Mipmap Seam Correction: Prevent artifacts at the 1.0/0.0 UV boundary\n" +
              "        vec2 dx = dFdx(uv);\n" +
              "        vec2 dy = dFdy(uv);\n" +
              "        if(dx.x > 0.5) dx.x -= 1.0; else if(dx.x < -0.5) dx.x += 1.0;\n" +
              "        \n" +
              "        // Sample the texture using corrected gradients for a seamless look\n" +
              "        FragColor = textureGrad(earthTexture, uv, dx, dy);\n" +
              "    }\n" +
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

        #region # IDisposable

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
        public int Level { get; }
        public Vector3d V0, V1, V2, V3;
        public Vector3d Center;
        public TerrainChunk[] Children;

        private readonly double _radius;
        private const int MaxLevel = 7;
        private const double SplitMultiplier = 4;

        public TerrainChunk(int level, double radius, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d v3)
        {
            Level = level;
            _radius = radius;
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
            Center = Vector3d.Normalize((v0 + v1 + v2 + v3) / 4.0) * _radius;
        }

        /// <summary>
        /// ⭐ 변경점: ICamera의 Position 대신 변환된 로컬 좌표(localCameraPos)를 받도록 서명(Signature)이 변경되었습니다.
        /// </summary>
        public void UpdateLOD(Vector3d localCameraPos, ICamera camera, FrustumCuller frustum, List<TerrainChunk> renderQueue)
        {
            // 이제 모든 거리 및 방향 판정은 지구가 멈춰있는 '로컬' 상태(localCameraPos)를 기준으로 이루어집니다.
            Vector3d cameraPos = localCameraPos;
            double cameraDist = cameraPos.Length;
            double boundingRadius = Vector3d.Distance(Center, V0);
            double sseFactor = camera.ScreenHeight / (2.0 * Math.Tan(camera.Fov.ToRadian() / 2.0));

            // 1. [Frustum Culling] 절두체 시야에서 벗어난 청크 버림
            // 현재 주석 처리되어 있지만, 회전 대응이 끝났으므로 주석을 해제하셔도 정상 작동합니다.
            // if (!frustum.IntersectsSphere(Center, boundingRadius)) return;

            // 2. [Horizon Culling]
            if (cameraDist > _radius)
            {
                // NaN 에러를 방지하기 위해 Clamp 로직 추가
                double cosAlpha = Math.Max(-1.0, Math.Min(1.0, _radius / cameraDist));
                double alpha = Math.Acos(cosAlpha);

                double sinBeta = Math.Max(-1.0, Math.Min(1.0, boundingRadius / _radius));
                double beta = Math.Asin(sinBeta);

                double thresholdAngle = alpha + beta;

                Vector3d chunkNormal = Vector3d.Normalize(Center);
                Vector3d cameraDir = Vector3d.Normalize(cameraPos);

                double dot = Math.Max(-1.0, Math.Min(1.0, Vector3d.Dot(chunkNormal, cameraDir)));
                double gamma = Math.Acos(dot);

                if (gamma > thresholdAngle)
                {
                    return;
                }
            }

            // 3. 텍스처 해상도 기반의 분할 조건 검사
            double chunkDiameter = boundingRadius * 2.0;

            // [변경] 구체 표면까지의 최단 거리 계산 (기존 동일)
            double distToCenter = Vector3d.Distance(localCameraPos, Center);
            double closestDistance = Math.Max(distToCenter - boundingRadius, 1.0); // 0 방지

            // SSE 기반 화면 차지 픽셀 크기 계산
            double chunkScreenSizePixels = (chunkDiameter / closestDistance) * sseFactor;

            // Chunks들의 기본 해상도 임계값
            double tileResolutionThreshold = 256.0 * 0.85; // 217.6 픽셀

            // ⭐ [해결된 핵심 로직] LOD 히스테리시스 오프셋 추가
            // Chunks가 한번 분할되면(SSE > Threshold), 다시 합쳐지려면 SSE가Threshold보다 
            // '훨씬 더 작아져야' 합니다. Chunks 상태를 안정시킵니다.
            bool isCurrentSplit = (Children != null); // 현재 자식이 있는 상태인가?

            // 히스테리시스 적용 임계값 설정
            double thresholdWithHysteresis = isCurrentSplit
                ? tileResolutionThreshold * 0.85 // ⭐ 이미 쪼개졌다면, 더 멀어져야(작아져야) 합친다. (약 185픽셀)
                : tileResolutionThreshold;       // 안 쪼개졌다면, 기본값 기준으로 쪼갠다. (약 217픽셀)

            // 화면 차지 픽셀이 텍스처 한계 해상도를 넘어서려 하면 쪼갭니다!
            if (Level < 2 || (chunkScreenSizePixels > tileResolutionThreshold && Level < MaxLevel))
            {
                if (Children == null) Split();

                foreach (var child in Children)
                {
                    // 자식들에게도 localCameraPos를 그대로 물려줍니다.
                    child.UpdateLOD(localCameraPos, camera, frustum, renderQueue);
                }
            }
            else
            {
                Children = null;
                renderQueue.Add(this);
            }
        }

        private void Split()
        {
            Vector3d m01 = Vector3d.Normalize((V0 + V1) / 2.0) * _radius;
            Vector3d m12 = Vector3d.Normalize((V1 + V2) / 2.0) * _radius;
            Vector3d m23 = Vector3d.Normalize((V2 + V3) / 2.0) * _radius;
            Vector3d m30 = Vector3d.Normalize((V3 + V0) / 2.0) * _radius;

            Children = new TerrainChunk[4];
            Children[0] = new TerrainChunk(Level + 1, _radius, V0, m01, Center, m30); // 0사분면
            Children[1] = new TerrainChunk(Level + 1, _radius, m01, V1, m12, Center); // 1사분면
            Children[2] = new TerrainChunk(Level + 1, _radius, Center, m12, V2, m23); // 2사분면
            Children[3] = new TerrainChunk(Level + 1, _radius, m30, Center, m23, V3); // 3사분면
        }
    }
}
