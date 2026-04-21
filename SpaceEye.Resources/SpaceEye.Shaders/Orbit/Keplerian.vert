#version 430 core
#extension GL_ARB_gpu_shader_fp64 : enable

uniform dmat4 view;
uniform dmat4 projection;

// [Binding = 1] Compute Shader가 방금 구워낸 3D 좌표 데이터 (Read-Only)
layout(std430, binding = 1) readonly buffer PositionBuffer {
    dvec4 orbitPositions[]; 
};

// [Binding = 2] 현재 뷰포트(SceneRenderer)의 위성 가시성 마스크 (1=Draw, 0=Hide)
layout(std430, binding = 2) readonly buffer VisibilityBuffer {
    int isVisible[]; 
};

void main()
{
    // 현재 그리고 있는 위성의 인덱스
    int satIndex = gl_InstanceID;

    // ?? 핵심 최적화: 조기 컬링 (Early Culling)
    if (isVisible[satIndex] == 0) 
    {
        // 정점을 클립 공간(화면) 밖으로 던져버려 픽셀 렌더링(Fragment) 단계를 아예 생략함
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0); 
        return;
    }

    // 가시성이 1인 경우 정상 렌더링
    dvec4 worldPos = orbitPositions[satIndex];
    
    // FP64(double) 정밀도로 행렬 곱셈 후, 최종 화면 투영 시에만 float으로 변환
    vec4 clipPos = vec4(projection * view * worldPos);
    
    gl_Position = clipPos;
    
    // 점의 크기 (필요시 uniform으로 외부에서 조절 가능)
    gl_PointSize = 3.0;
}