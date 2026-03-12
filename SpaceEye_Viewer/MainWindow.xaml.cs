using SpaceEye.Common.CelestialDefinition;
using SpaceEye.Scene;
using SpaceEye_Viewer.Celestial;
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

namespace SpaceEye_Viewer
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool _isUniverseInitialized = false;

        public MainWindow()
        {
            InitializeComponent();

            // 1. 여기서 SharedUniverse를 먼저 확실히 생색해둡니다.
            EarthControl.SharedUniverse = new UniverseScene();

            // 2. 이벤트를 기다리지 않고 '보여질 때' 즉시 실행되도록 강제 예약
            this.Activated += (s, e) => {
                if (!_isUniverseInitialized)
                {
                    System.Diagnostics.Debug.WriteLine(">>> Window Activated! 초기화 시도.");
                    InitializeSceneNodes();
                }
            };
        }

        private async void InitializeSceneNodes()
        {
            if (_isUniverseInitialized) return;

            // 3. UI가 완전히 그려질 시간을 줍니다.
            await System.Threading.Tasks.Task.Delay(500);

            // 4. Dispatcher를 쓰지 않고 Invoke로 즉시 실행 시도
            try
            {
                this.Dispatcher.Invoke(() =>
                {                    
                    // 노드 직접 생성 및 추가
                    var node = new EarthNode();                 
                    EarthControl.SharedUniverse.AddNode(node);                 
                    SetupInitialCameras();
                    _isUniverseInitialized = true;                  
                    // 강제 화면 갱신
                    SpaceEyeViewControl.InvalidateVisual();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! 초기화 중 에러: {ex.Message}");
            }
        }

        /// <summary>
        /// 각 뷰포트(컨트롤)의 초기 카메라 위치를 설정합니다.
        /// </summary>
        private void SetupInitialCameras()
        {
            double earthRadius = Earth.EarthRadius;

            // 왼쪽 창: 멀리서 지구 전체 보기 (고도 20,000km)
            var cam = SpaceEyeViewControl.Renderer.ViewCamera;
            cam.Target = OpenTK.Vector3d.Zero;
            cam.Position = new OpenTK.Vector3d(0, 0, earthRadius + 20000.0);
            cam.Up = OpenTK.Vector3d.UnitY;
        }
    }
}
