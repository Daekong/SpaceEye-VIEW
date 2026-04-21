using System;
using System.Windows;
using System.Windows.Controls;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenTK.Graphics;
using SpaceEye.Scene;
using SpaceEye.Renderer;
using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Common.Scene;
using SpaceEye.Common.Interfaces;
using SpaceEye.Scene.Common;
using SpaceEye.Scene.Celestial;

namespace SpaceEye_Viewer.Celestial
{
    /// <summary>
    /// EarthControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EarthControl : UserControl
    {
        #region # Fields

        private bool _isUniverseInitialized = false;

        /// <summary>
        /// 모든 <see cref="EarthControl"/> 인스턴스가 공유하여 렌더링할 통합 우주 씬입니다.
        /// </summary>
        /// <remarks>
        /// 프로그램 시작 시 <see cref="UniverseScene"/> 객체를 생성하여 이 프로퍼티에 할당해야 합니다.
        /// </remarks>
        private UniverseScene SharedUniverse { get; set; } = UniverseScene.Instance;

        /// <summary>
        /// 이 컨트롤만의 독립적인 64비트 렌더러입니다. 카메라 정보를 포함하고 있습니다.
        /// </summary>
        private readonly SceneRenderer _renderer = new SceneRenderer();

        /// <summary>
        /// 현재 컨트롤의 시점을 제어하는 렌더러에 접근하기 위한 프로퍼티입니다.
        /// </summary>
        internal SceneRenderer Renderer => _renderer;

        /// <summary>
        /// 마지막 업데이트 된 시각
        /// </summary>
        private DateTime _lastTime = DateTime.Now;

        #endregion

        #region # Propertie

        /// <summary>
        /// 우주 배경색 (R, G, B, A)
        /// </summary>
        public float[] BackgroundColor { get; set; } = new float[] { 0.02f, 0.02f, 0.05f, 1.0f };

        #endregion

        #region # Constructor

        /// <summary>
        /// OpenTK 렌더링 환경을 직접 호스팅하고 지구 전용 UI를 제공하는 사용자 정의 컨트롤입니다.
        /// </summary>
        /// <remarks>
        /// 3D 뷰포트 기능과 오버레이 UI가 하나의 클래스에 직관적으로 통합되어 있습니다.
        /// </remarks>
        public EarthControl()
        {
            InitializeComponent();

            // 1. 여기서 직접 Render 이벤트를 강력하게 연결합니다!
            GlControl.Render += GlControl_Render;

            // 화면 로드가 완료된 시점에 렌더링을 시작하도록 이벤트 등록
            this.Loaded += EarthControl_Loaded;
            GlControl.MouseWheel += OnMouseWheel;
        }

        #endregion

        #region # Events

        /// <summary>
        /// 컨트롤이 화면에 로드될 때 발생하는 이벤트 핸들러로, OpenGL 렌더링 환경을 초기화합니다.
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 객체</param>
        /// <param name="e">이벤트 관련 데이터</param>
        private void EarthControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Dispatcher를 사용하여 UI 렌더링 준비가 완전히 끝난 '후'에 실행되도록 예약합니다.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var settings = new GLWpfControlSettings
                    {
                        MajorVersion = 4,
                        MinorVersion = 1, // 호환성을 위해 4.1
                        RenderContinuously = true
                    };

                    GlControl.Start(settings);

                    // 만약 그래도 안 된다면, 강제로 무효화하여 다시 그리게 만듭니다.
                    GlControl.InvalidateVisual();

                    InitializeSceneNodes();                   
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);          
        }

        #endregion

        #region # Private Method

        /// <summary>
        /// 매 프레임마다 발생하는 렌더링 이벤트 핸들러입니다.
        /// </summary>
        /// <param name="timeDelta">이전 프레임으로부터 경과된 시간 (<see cref="TimeSpan"/>)</param>
        private void GlControl_Render(TimeSpan timeDelta)
        {
            if (SharedUniverse == null) return;

            // --- DPI 보정 로직 시작 ---
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null && source.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // 실제 픽셀 크기 (ActualWidth * DPI 배율)
            double pWidth = ActualWidth * dpiX;
            double pHeight = ActualHeight * dpiY;

            // ---------------------------------------------------------

            if (pWidth <= 0 || pHeight <= 0) return;

            // 1. [핵심] 물리 업데이트 (Update 단계)
            // 렌더링 직전에 현재 시간과 이전 프레임 시간의 차이를 계산해 넘겨줍니다.
            DateTime currentTime = DateTime.Now;
            double deltaSeconds = (currentTime - _lastTime).TotalSeconds;
            _lastTime = currentTime;

            // UniverseScene 싱글톤을 통해 ITimeUpdateable 노드들의 Update(deltaSeconds) 일괄 호출
            UniverseScene.Instance.UpdateAll(deltaSeconds, _renderer.ViewCamera as ICamera);

            // ---------------------------------------------------------

            // 2. [핵심] 렌더링 (Draw 단계)
            // 업데이트된 데이터를 기반으로 화면을 그립니다.
            if (pWidth <= 0 || pHeight <= 0) return;

            _renderer.Render(UniverseScene.Instance, pWidth, pHeight);        

            // 3. 루프 유지
            Dispatcher.BeginInvoke(new Action(() => GlControl.InvalidateVisual()),
                                  System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 각 뷰포트(컨트롤)의 초기 카메라 위치를 설정합니다.
        /// </summary>
        private void SetupInitialCameras()
        {
            double earthRadius = Earth.EarthRadius;

            // 왼쪽 창: 멀리서 지구 전체 보기 (고도 20,000km)
            var cam = Renderer.ViewCamera;
            cam.Target = OpenTK.Vector3d.Zero;
            cam.Position = new OpenTK.Vector3d(0, 0, Earth.EarthCameraMaxDistance);
            cam.Up = OpenTK.Vector3d.UnitY;
        }

        /// <summary>
        /// Control의 초기 Scene을 구성합니다.
        /// </summary>
        private void InitializeSceneNodes()
        {
            if (_isUniverseInitialized) return;           

            // 4. Dispatcher를 쓰지 않고 Invoke로 즉시 실행 시도
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    // 노드 직접 생성 및 추가
                    UniverseScene.Instance.AddNode(new SkyboxNode());                   
                    UniverseScene.Instance.AddNode(new EarthNodeLod());
                    SetupInitialCameras();
                    _isUniverseInitialized = true;                    
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! 초기화 중 에러: {ex.Message}");
            }
        }

        #endregion

        #region # Events 

        /// <summary>
        /// 마우스 휠 스크롤 시 호출되어 카메라 줌을 수행합니다.
        /// </summary>
        private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // e.Delta는 보통 위로 굴릴 때 +120, 아래로 굴릴 때 -120이 들어옵니다.
            _renderer.ViewCamera.Zoom(e.Delta);

            // 이벤트 처리가 완료되었음을 시스템에 알림
            e.Handled = true;

            // 화면을 즉시 갱신해야 한다면 렌더링 트리거를 호출하세요. (예: glControl.Invalidate();)
        }

        #endregion
    }
}
