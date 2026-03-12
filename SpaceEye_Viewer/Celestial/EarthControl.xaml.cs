using System;
using System.Windows;
using System.Windows.Controls;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenTK.Graphics;

namespace SpaceEye_Viewer.Celestial
{
    /// <summary>
    /// EarthControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EarthControl : UserControl
    {
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
                        MajorVersion = 3,
                        MinorVersion = 0, // 호환성을 위해 3.0
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
            // 1. 지정된 배경색으로 이전 프레임의 잔상을 깨끗하게 지웁니다.
            GL.ClearColor(BackgroundColor[0], BackgroundColor[1], BackgroundColor[2], BackgroundColor[3]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // TODO: SpaceEye.Core.Camera 정보 갱신
            // TODO: SpaceEye.Scene.EarthScene의 렌더링 로직(Draw) 호출
        }

        #endregion
    }
}
