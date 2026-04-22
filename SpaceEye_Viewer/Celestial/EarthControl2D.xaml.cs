using OpenTK;
using OpenTK.Wpf;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Interfaces;
using SpaceEye.Common.Scene;
using SpaceEye.Renderer;
using SpaceEye.Scene.Celestial;
using SpaceEye.Scene.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SpaceEye_Viewer.Celestial
{
    /// <summary>
    /// 2D 직교 투영 기반의 우주 공간을 시각화하고 사용자 입력을 처리하는 컨트롤입니다.
    /// </summary>
    public partial class EarthControl2D : UserControl
    {
        #region # Private Fields

        /// <summary>우주 씬 노드(지구, 스카이박스 등)의 초기화 여부를 나타냅니다.</summary>
        private static bool _isUniverseInitialized = false;

        /// <summary>이 컨트롤의 2D 렌더링을 담당하는 내부 렌더러입니다.</summary>
        private readonly SceneRenderer2D _renderer = new SceneRenderer2D();

        /// <summary>이전 프레임의 시간을 저장하여 시간 델타를 계산하는 데 사용합니다.</summary>
        private DateTime _lastTime = DateTime.Now;

        /// <summary>마우스 드래그 이동 시 마지막 마우스 위치를 저장합니다.</summary>
        private Point _lastMousePosition;

        /// <summary>현재 마우스 드래그(Panning)가 진행 중인지 여부를 나타냅니다.</summary>
        private bool _isDragging = false;

        #endregion

        #region # Public Properties

        /// <summary>이 컨트롤의 시점을 제어하는 렌더러 객체를 가져옵니다.</summary>
        internal SceneRenderer2D Renderer => _renderer;

        /// <summary>시간 흐름에 따른 애니메이션/물리 업데이트 수행 여부를 가져오거나 설정합니다.</summary>
        public bool IsTimeUpdate { get; set; } = true;

        /// <summary>시간 흐름의 배율을 설정합니다. (예: 1.0 = 실시간)</summary>
        public double TimeScale { get; set; } = 1000.0;

        #endregion

        /// <summary>
        /// <see cref="EarthControl2D"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        public EarthControl2D()
        {
            InitializeComponent();

            GlControl.Render += GlControl_Render;
            GlControl.MouseWheel += OnMouseWheel;
            GlControl.MouseDown += OnMouseDown;
            GlControl.MouseMove += OnMouseMove;
            GlControl.MouseUp += OnMouseUp;

            this.Loaded += EarthControl2D_Loaded;
        }

        /// <summary>
        /// 컨트롤이 로드될 때 OpenGL 환경 및 씬 노드를 초기화합니다.
        /// </summary>
        /// <param name="sender">이벤트 발생 객체입니다.</param>
        /// <param name="e">이벤트 데이터입니다.</param>
        private void EarthControl2D_Loaded(object sender, RoutedEventArgs e)
        {
            // 뷰포트(Control)가 생성될 때마다 각자의 OpenGL 렌더링을 시작합니다.
            var settings = new GLWpfControlSettings
            {
                MajorVersion = 4,
                MinorVersion = 3,
                RenderContinuously = true
            };

            try
            {
                GlControl.Start(settings);
                GlControl.InvalidateVisual();

                // 렌더링 시작 후 씬 데이터를 초기화하러 갑니다.
                InitializeSceneNodes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"2D Viewport Start Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 매 프레임마다 물리 업데이트 및 렌더링을 수행합니다.
        /// </summary>
        /// <param name="timeDelta">프레임 간 경과 시간입니다.</param>
        private void GlControl_Render(TimeSpan timeDelta)
        {
            if (UniverseScene.Instance == null) return;

            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double pWidth = ActualWidth * dpiX;
            double pHeight = ActualHeight * dpiY;

            if (pWidth <= 0 || pHeight <= 0) return;

            if (IsTimeUpdate)
            {
                DateTime currentTime = DateTime.Now;
                double deltaSeconds = (currentTime - _lastTime).TotalSeconds;
                _lastTime = currentTime;

                UniverseScene.Instance.UpdateAll(deltaSeconds * TimeScale, _renderer.ViewCamera as ICamera);
            }

            _renderer.Render(UniverseScene.Instance, pWidth, pHeight);

            Dispatcher.BeginInvoke(new Action(() => GlControl.InvalidateVisual()), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 카메라의 초기 위치를 설정하고 공유 씬 노드를 초기화합니다.
        /// </summary>
        private void InitializeSceneNodes()
        {
            SetupInitialCameras();

            if (_isUniverseInitialized) return;

            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (_isUniverseInitialized) return;
                    
                    UniverseScene.Instance.AddNode(new EarthNodeTES2D());
                    _isUniverseInitialized = true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"2D 초기화 에러: {ex.Message}");
            }
        }

        /// <summary>마우스 휠 스크롤을 통해 줌 인/아웃을 처리합니다.</summary>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _renderer.ViewCamera.Zoom(e.Delta);
            e.Handled = true;
        }

        /// <summary>마우스 왼쪽 버튼 클릭 시 드래그 이동을 시작합니다.</summary>
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(this);
                GlControl.CaptureMouse();
            }
        }

        /// <summary>마우스 이동 시 드래그 중이라면 카메라를 평면 이동시킵니다.</summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPos = e.GetPosition(this);
                Vector diff = currentPos - _lastMousePosition;

                double pixelToKm = _renderer.ViewCamera.Fov / ActualHeight;
                _renderer.ViewCamera.Move(-diff.X * pixelToKm, diff.Y * pixelToKm);

                _lastMousePosition = currentPos;
            }
        }

        /// <summary>마우스 버튼을 떼면 드래그 상태를 해제합니다.</summary>
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            GlControl.ReleaseMouseCapture();
        }

        /// <summary>
        /// 각 뷰포트(컨트롤)의 초기 카메라 위치를 설정합니다.
        /// </summary>
        private void SetupInitialCameras()
        {
            var cam = _renderer.ViewCamera;
            cam.Target = Vector3d.Zero;
            cam.Position = new Vector3d(0, 0, 1000);
            cam.Fov = 20000.0;
        }
    }
}
