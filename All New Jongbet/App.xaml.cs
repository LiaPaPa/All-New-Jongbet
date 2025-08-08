// App.xaml.cs 파일 전체를 아래 코드로 교체하세요.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace All_New_Jongbet
{
    public partial class App : Application
    {
        // NEW: 프로그램 전역에서 사용할 디버그 모드 플래그
        public static bool IsDebugMode { get; private set; } = false;

        public App()
        {
            // C#의 전처리기 지시문을 사용하여
            // Visual Studio에서 'Debug' 모드로 컴파일할 때만 IsDebugMode를 true로 설정합니다.
#if DEBUG
            IsDebugMode = true;
#endif
        }
    }
}