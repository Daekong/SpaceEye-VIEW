using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEye.Core.Common
{
    /// <summary>
    /// OpenGL 셰이더 코드를 컴파일하고 프로그램으로 링크하는 유틸리티 클래스입니다.
    /// </summary>
    internal static class ShaderCompiler
    {
        /// <summary>
        /// 정점(Vertex) 셰이더와 단편(Fragment) 셰이더 소스 코드를 컴파일하여 하나의 셰이더 프로그램으로 생성합니다.
        /// </summary>
        /// <param name="vertexSource">컴파일할 정점 셰이더의 GLSL 소스 코드입니다.</param>
        /// <param name="fragmentSource">컴파일할 단편 셰이더의 GLSL 소스 코드입니다.</param>
        /// <returns>생성된 OpenGL 셰이더 프로그램의 식별자(ID)를 반환합니다.</returns>
        /// <exception cref="Exception">셰이더 컴파일 또는 프로그램 링크 중 오류가 발생할 경우 예외를 던집니다.</exception>
        public static int CreateProgram(string vertexSource, string fragmentSource)
        {
            // 1. 각각의 셰이더 생성 및 컴파일
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            // 2. 셰이더 프로그램 생성 및 링크
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);

            // 3. 링크 오류 검사
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"셰이더 프로그램 링크 실패:\n{infoLog}");
            }

            // 4. 링크가 완료된 개별 셰이더는 메모리 해제 (프로그램에 이미 포함됨)
            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        /// <summary>
        /// 외부 셰이더 파일의 경로를 입력받아 소스 코드를 읽은 후, 테셀레이션 셰이더 프로그램으로 컴파일하고 링크합니다.
        /// </summary>
        /// <param name="vertexPath">정점 셰이더 파일 경로</param>
        /// <param name="tcsPath">테셀레이션 제어 셰이더(TCS) 파일 경로</param>
        /// <param name="tesPath">테셀레이션 평가 셰이더(TES) 파일 경로</param>
        /// <param name="fragmentPath">단편 셰이더 파일 경로</param>
        /// <returns>생성된 테셀레이션 셰이더 프로그램의 식별자(ID)</returns>
        public static int CreateProgramFromFiles(string vertexPath, string tcsPath, string tesPath, string fragmentPath)
        {
            // 1. 파일에서 텍스트(소스 코드) 읽어오기
            string vertexSource = System.IO.File.ReadAllText(vertexPath, System.Text.Encoding.UTF8);
            string tcsSource = System.IO.File.ReadAllText(tcsPath, System.Text.Encoding.UTF8);
            string tesSource = System.IO.File.ReadAllText(tesPath, System.Text.Encoding.UTF8);
            string fragmentSource = System.IO.File.ReadAllText(fragmentPath, System.Text.Encoding.UTF8);

            // 2. 기존 문자열 기반 CreateProgram 함수 호출하여 반환
            return CreateProgram(vertexSource, tcsSource, tesSource, fragmentSource);
        }

        /// <summary>
        /// 정점(VS), 테셀레이션 제어(TCS), 테셀레이션 평가(TES), 단편(FS) 셰이더를 모두 포함하여 컴파일하고 링크합니다.
        /// </summary>
        /// <param name="vertexSource">컴파일할 정점 셰이더의 GLSL 소스 코드입니다.</param>
        /// <param name="tcsSource">컴파일할 테셀레이션 제어 셰이더(TCS)의 GLSL 소스 코드입니다.</param>
        /// <param name="tesSource">컴파일할 테셀레이션 평가 셰이더(TES)의 GLSL 소스 코드입니다.</param>
        /// <param name="fragmentSource">컴파일할 단편 셰이더의 GLSL 소스 코드입니다.</param>
        /// <returns>생성된 테셀레이션 셰이더 프로그램의 식별자(ID)를 반환합니다.</returns>
        /// <exception cref="Exception">셰이더 컴파일 또는 프로그램 링크 중 오류가 발생할 경우 예외를 던집니다.</exception>
        /// <remarks>
        /// 하드웨어 테셀레이션을 수행하기 위해 그래픽 파이프라인에 TCS와 TES 단계를 추가로 삽입합니다.
        /// 링크가 완료된 후 개별 셰이더 객체는 메모리에서 자동으로 해제됩니다.
        /// </remarks>
        public static int CreateProgram(string vertexSource, string tcsSource, string tesSource, string fragmentSource)
        {
            // 1. 4개의 셰이더를 각각 생성 및 컴파일
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int tcsShader = CompileShader(ShaderType.TessControlShader, tcsSource);
            int tesShader = CompileShader(ShaderType.TessEvaluationShader, tesSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            // 2. 셰이더 프로그램 생성 및 모두 부착(Attach)
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, tcsShader);
            GL.AttachShader(program, tesShader);
            GL.AttachShader(program, fragmentShader);

            // 3. 프로그램 링크
            GL.LinkProgram(program);

            // 4. 링크 오류 검사
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"테셀레이션 셰이더 프로그램 링크 실패:\n{infoLog}");
            }

            // 5. 메모리 최적화: 링크가 완료된 개별 셰이더 분리 및 삭제
            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, tcsShader);
            GL.DetachShader(program, tesShader);
            GL.DetachShader(program, fragmentShader);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(tcsShader);
            GL.DeleteShader(tesShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        /// <summary>
        /// 단일 셰이더 소스 코드를 컴파일합니다.
        /// </summary>
        private static int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            // 컴파일 오류 검사
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"{type} 컴파일 실패:\n{infoLog}");
            }

            return shader;
        }
    }
}
