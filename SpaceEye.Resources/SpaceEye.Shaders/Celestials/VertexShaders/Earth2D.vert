#version 430 core

/**
 * @file Earth2D.vert
 * @brief 2D 지도를 위한 정점 셰이더입니다.
 * @remarks 
 * CPU로부터 받은 64비트(double) 데이터를 테셀레이션 제어 셰이더(TCS)로 전달합니다.
 * 실제 좌표 변환은 테셀레이션 평가 셰이더(TES)에서 수행됩니다.
 */

// 64비트 정밀도 위치 데이터 (X, Y, Z)
layout (location = 0) in dvec3 aPos;
// 64비트 정밀도 UV 데이터 (U, V)
layout (location = 1) in dvec2 aTexCoord;

// TCS로 전달할 출력 구조체
out dvec3 vPosTCS;
out dvec2 vTexCoordTCS;

void main() {
    /** 
     * @remarks 테셀레이션 단계로 원본 데이터를 그대로 전달합니다. 
     */
    vPosTCS = aPos;
    vTexCoordTCS = aTexCoord;
}