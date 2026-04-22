#version 430 core

/**
 * @file Earth2D.frag
 * @brief 2D 지구 지도의 색상을 결정합니다.
 */

layout (location = 0) out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D earthTexture;
uniform float uOffset;
uniform bool isWireframe;
uniform vec3 wireColor;

void main() {
    //// 무한 스핀 적용 (uOffset 강제 참조)
    //vec2 uv = vec2(vTexCoord.x + uOffset, vTexCoord.y);
    //
    //// Mipmap 경계면 보정
    //vec2 dx = dFdx(uv);
    //vec2 dy = dFdy(uv);
    //if(dx.x > 0.5) dx.x -= 1.0; else if(dx.x < -0.5) dx.x += 1.0;
    //
    //vec4 texColor = textureGrad(earthTexture, uv, dx, dy);
    //
    //if (isWireframe) {
    //    // wireColor를 명시적으로 출력하여 최적화 삭제 방지
    //    FragColor = vec4(wireColor, 1.0);
    //} else {
    //    // 텍스처 컬러 출력
    //    FragColor = texColor;
    //}

    // [중요] 만약 여전히 검은색이라면 아래 한 줄만 남기고 위를 모두 주석처리 해보세요.
     FragColor = vec4(1.0, 0.0, 0.0, 1.0); // 빨간색이 나오면 렌더링 파이프라인은 정상입니다.
}