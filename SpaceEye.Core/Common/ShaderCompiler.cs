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
