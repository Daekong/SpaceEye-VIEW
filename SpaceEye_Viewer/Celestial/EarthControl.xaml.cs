using System;
using System.Windows;
using System.Windows.Controls;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenTK.Graphics;
using SpaceEye.Scene;
using SpaceEye.Renderer;

namespace SpaceEye_Viewer.Celestial
{
    /// <summary>
    /// EarthControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EarthControl : UserControl
    {
        #region # Fields

        /// <summary>
        /// 모든 <see cref="EarthControl"/> 인스턴스가 공유하여 렌더링할 통합 우주 씬입니다.
        /// </summary>
        /// <remarks>
        /// 프로그램 시작 시 <see cref="UniverseScene"/> 객체를 생성하여 이 프로퍼티에 할당해야 합니다.
        /// </remarks>
        internal static UniverseScene SharedUniverse { get; set; }

        /// <summary>
        /// 이 컨트롤만의 독립적인 64비트 렌더러입니다. 카메라 정보를 포함하고 있습니다.
        /// </summary>
        private readonly SceneRenderer _renderer = new SceneRenderer();

        /// <summary>
        /// 현재 컨트롤의 시점을 제어하는 렌더러에 접근하기 위한 프로퍼티입니다.
        /// </summary>
        internal SceneRenderer Renderer => _renderer;

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

                    System.Diagnostics.Debug.WriteLine("OpenTK Start() has been called.");
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

            // 1. 렌더링 수행
            _renderer.Render(SharedUniverse, ActualWidth, ActualHeight);

            // 2. [수정] 무조건적인 Invoke 대신, 렌더링 우선순위를 최하위로 낮추거나 
            // CompositionTarget.Rendering 이벤트를 활용하는 것이 정석입니다.
            // 일단 가장 간단한 해결책은 Priority를 최하위로 낮추는 것입니다.
            Dispatcher.BeginInvoke(new Action(() => {
                // 이 코드가 UI 스레드에 너무 자주 쌓이지 않도록 방지
                GlControl.InvalidateVisual();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion
    }
}
