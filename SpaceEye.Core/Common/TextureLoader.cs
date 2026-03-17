using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Shapes;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace SpaceEye.Core.Common
{
    /// <summary>
    /// 외부 이미지 파일을 읽어 OpenGL 텍스처 리소스로 변환하는 유틸리티 클래스입니다.
    /// </summary>
    internal static class TextureLoader
    {
        /// <summary>
        /// 6장의 개별 이미지를 로드하여 하나의 큐브맵(Cubemap) 텍스처로 병합하고 GPU에 할당합니다.
        /// </summary>
        /// <param name="facePaths">
        /// 큐브맵의 6면을 구성하는 이미지 파일 경로 배열입니다. 
        /// 반드시 다음 순서로 6개의 경로가 포함되어야 합니다:
        /// <list type="number">
        /// <item><description>오른쪽 (Right, +X)</description></item>
        /// <item><description>왼쪽 (Left, -X)</description></item>
        /// <item><description>위 (Top, +Y)</description></item>
        /// <item><description>아래 (Bottom, -Y)</description></item>
        /// <item><description>앞 (Front, +Z)</description></item>
        /// <item><description>뒤 (Back, -Z)</description></item>
        /// </list>
        /// </param>
        /// <returns>생성된 큐브맵 텍스처의 OpenGL 식별자(ID)를 반환합니다.</returns>
        /// <exception cref="ArgumentException">배열의 길이가 6이 아닐 경우 발생합니다.</exception>
        /// <exception cref="FileNotFoundException">지정된 경로의 파일을 찾을 수 없을 경우 발생합니다.</exception>
        /// <remarks>
        /// 이 메서드는 <see cref="TextureTarget.TextureCubeMap"/>을 사용하여 텍스처를 바인딩하며,
        /// 큐브맵 특성상 모서리 부분의 경계선이 보이지 않도록 <see cref="TextureWrapMode.ClampToEdge"/>를 적용합니다.
        /// </remarks>
        public static int LoadCubemap(string[] facePaths)
        {
            if (facePaths == null || facePaths.Length != 6)
            {
                throw new ArgumentException("큐브맵을 생성하려면 정확히 6개의 이미지 경로가 필요합니다.", nameof(facePaths));
            }

            // 1. 텍스처 ID 생성 및 큐브맵으로 바인딩
            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, textureId);

            // 2. 6개의 이미지를 순회하며 GPU 메모리에 로드
            for (int i = 0; i < facePaths.Length; i++)
            {
                if (!File.Exists(facePaths[i]))
                {
                    throw new FileNotFoundException($"큐브맵 이미지를 찾을 수 없습니다: {facePaths[i]}");
                }

                // 비트맵 이미지 로드
                using (Bitmap image = new Bitmap(facePaths[i]))
                {
                    // 메모리 락을 걸어 픽셀 데이터에 안전하게 접근
                    BitmapData data = image.LockBits(
                        new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                        ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    // OpenGL 텍스처 타겟 계산 (+X부터 -Z까지 순차적으로 할당)
                    TextureTarget target = TextureTarget.TextureCubeMapPositiveX + i;

                    // 픽셀 데이터를 GPU로 전송
                    GL.TexImage2D(
                        target,
                        0,
                        PixelInternalFormat.Rgba,
                        image.Width,
                        image.Height,
                        0,
                        PixelFormat.Bgra, // Bitmap의 기본 포맷은 BGRA입니다.
                        PixelType.UnsignedByte,
                        data.Scan0);

                    image.UnlockBits(data);
                }
            }

            // 3. 텍스처 필터링 및 래핑(Wrapping) 파라미터 설정
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // 큐브의 모서리(Edge)에서 텍스처가 끊어져 보이는 선(Seam)을 방지
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            return textureId;
        }

        /// <summary>
        /// 2D 이미지 파일을 읽어 OpenGL 텍스처로 변환하고 식별자(ID)를 반환합니다.
        /// </summary>
        /// <param name="path">텍스처 이미지 파일의 상대 또는 절대 경로</param>
        /// <returns>생성된 OpenGL 텍스처 ID</returns>
        public static int LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"텍스처 파일을 찾을 수 없습니다: {path}");
            }

            // 1. 텍스처 ID 생성 및 바인딩
            int handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);

            // 2. 텍스처 래핑(Wrapping) 설정
            // 가로(S)는 지도가 이어지도록 Repeat, 세로(T)는 극지방이 깨지지 않도록 ClampToEdge 사용
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // 3. 텍스처 필터링(Filtering) 설정
            // 축소 시 부드럽게 보이도록 Mipmap 사용, 확대 시 선명하게 Linear 사용
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // 4. 비트맵 이미지 로드 및 GPU 메모리 전송
            using (Bitmap image = new Bitmap(path))
            {
                // ⭐ OpenGL은 이미지의 좌하단을 (0,0)으로 인식하므로 상하 반전이 필요할 수 있습니다.
                // 만약 지도가 위아래로 뒤집혀서 나온다면 아래 주석을 해제하세요.
                // image.RotateFlip(RotateFlipType.RotateNoneFlipY);

                // 고속 메모리 접근을 위해 이미지를 메모리에 잠금
                BitmapData data = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // CPU(Bitmap)의 픽셀 데이터를 GPU로 복사
                // C# Bitmap은 내부적으로 BGRA 순서로 픽셀을 저장하므로 PixelFormat.Bgra를 사용합니다.
                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    image.Width,
                    image.Height,
                    0,
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    data.Scan0);

                // 메모리 잠금 해제
                image.UnlockBits(data);
            }

            // 5. 밉맵(Mipmap) 자동 생성 (멀리 있는 지형을 그릴 때 성능 및 화질 최적화)
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // 상태 안전 해제
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return handle;
        }  

        /// <summary>
        /// 메모리에 로드된 6장의 비트맵(Bitmap) 이미지를 병합하여 하나의 큐브맵(Cubemap) 텍스처를 생성하고 GPU에 할당합니다.
        /// </summary>
        /// <param name="faces">
        /// 큐브맵의 6면을 구성하는 비트맵 이미지 배열입니다. 
        /// 반드시 다음 순서로 6개의 이미지가 포함되어야 합니다:
        /// <list type="number">
        /// <item><description>오른쪽 (Right, +X)</description></item>
        /// <item><description>왼쪽 (Left, -X)</description></item>
        /// <item><description>위 (Top, +Y)</description></item>
        /// <item><description>아래 (Bottom, -Y)</description></item>
        /// <item><description>앞 (Front, +Z)</description></item>
        /// <item><description>뒤 (Back, -Z)</description></item>
        /// </list>
        /// </param>
        /// <returns>생성된 큐브맵 텍스처의 OpenGL 식별자(ID)를 반환합니다.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="faces"/> 배열이 null일 경우 발생합니다.</exception>
        /// <exception cref="ArgumentException"><paramref name="faces"/> 배열의 길이가 정확히 6이 아닐 경우 발생합니다.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>이 메서드는 <see cref="Bitmap.LockBits(Rectangle, ImageLockMode, System.Drawing.Imaging.PixelFormat)"/>를 사용하여 메모리에 직접 접근하므로 처리 속도가 빠릅니다.</description></item>
        /// <item><description>큐브맵의 모서리(Edge)에서 텍스처가 끊어져 보이는 선(Seam)을 방지하기 위해 래핑 모드를 <see cref="TextureWrapMode.ClampToEdge"/>로 강제 설정합니다.</description></item>
        /// <item><description>메서드 호출이 끝난 후, 전달된 <see cref="Bitmap"/> 객체들의 메모리 해제(<c>Dispose</c>) 책임은 호출자에게 있습니다.</description></item>
        /// </list>
        /// </remarks>
        public static int LoadCubemap(Bitmap[] faces)
            {
                if (faces == null)
                {
                    throw new ArgumentNullException(nameof(faces), "비트맵 배열이 null일 수 없습니다.");
                }

                if (faces.Length != 6)
                {
                    throw new ArgumentException("큐브맵을 생성하려면 정확히 6개의 비트맵 이미지가 필요합니다.", nameof(faces));
                }

                // 1. 텍스처 ID 생성 및 바인딩
                int textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, textureId);

                // 2. 6개의 이미지를 순회하며 GPU 메모리에 로드
                for (int i = 0; i < faces.Length; i++)
                {
                    Bitmap image = faces[i];

                    // 메모리 락을 걸어 픽셀 데이터에 안전하고 빠르게 접근
                    BitmapData data = image.LockBits(
                        new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                        ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    // OpenGL 텍스처 타겟 계산 (+X부터 -Z까지 순차적으로 할당)
                    TextureTarget target = TextureTarget.TextureCubeMapPositiveX + i;

                    // 픽셀 데이터를 GPU로 전송 (Bitmap의 기본 포맷인 BGRA 사용)
                    GL.TexImage2D(
                        target,
                        0,
                        PixelInternalFormat.Rgba,
                        image.Width,
                        image.Height,
                        0,
                        PixelFormat.Bgra,
                        PixelType.UnsignedByte,
                        data.Scan0);

                    // 메모리 락 해제
                    image.UnlockBits(data);
                }

                // 3. 텍스처 필터링 및 래핑 파라미터 설정
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

                return textureId;
            }
    }
}
