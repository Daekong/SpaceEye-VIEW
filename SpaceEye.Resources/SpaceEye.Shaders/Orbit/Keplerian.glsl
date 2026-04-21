#version 430 core
// 64비트 배정밀도(FP64) 연산 활성화 (지터링 방지)
#extension GL_ARB_gpu_shader_fp64 : enable 

// 한 번의 워크 그룹(Work Group)에서 256개의 위성을 동시에 병렬 계산합니다.
layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// 1. C#의 GpuTleData 구조체와 1:1로 매칭되는 메모리 레이아웃
struct TleData {
    int noradId;
    float bStar;
    double inclination;
    double raan;
    double eccentricity;
    double argPerigee;
    double meanAnomaly;
    double meanMotion;
};

// [Binding = 0] OrbitManager가 올려준 원본 TLE 데이터 버퍼 (Read-Only)
layout(std430, binding = 0) readonly buffer TleBuffer {
    TleData satellites[];
};

// [Binding = 1] 계산 완료된 3D 좌표를 저장할 버퍼 (Write)
layout(std430, binding = 1) buffer PositionBuffer {
    dvec4 orbitPositions[]; 
};

// 우주의 현재 시간 (C#에서 매 프레임 업데이트해주는 Uniform 변수)
uniform double u_timeSinceEpoch; 

const double MU = 398600.4418; // 지구 표준 중력 변수 (km^3/s^2)

void main() 
{
    // 현재 스레드의 고유 ID = 위성의 인덱스
    uint idx = gl_GlobalInvocationID.x;
    
    // 버퍼 길이를 초과하는 잉여 스레드 실행 방지 로직 (필요시 추가)
    // if (idx >= u_activeSatelliteCount) return;

    TleData sat = satellites[idx];

    // --- [ GPU 궤도 수학 연산 (Baking) ] ---
    
    // 1. 평균 근점각 (현재 시간에 따른 위성의 평균 위치 추정)
    double n = sat.meanMotion; 
    double M = sat.meanAnomaly + n * u_timeSinceEpoch; 

    // 2. 이심 이근점각 (Eccentric Anomaly, E) - 뉴턴-랩슨(Newton-Raphson) 근사법 적용
    // GPU는 반복문(For)이 적을수록 유리하므로 5회 반복으로 충분한 정밀도 확보
    double E = M;
    for (int i = 0; i < 5; i++) {
        E = E - (E - sat.eccentricity * sin(E) - M) / (1.0 - sat.eccentricity * cos(E));
    }

    // 3. 진근점각 (True Anomaly, nu) 계산
    double cosNu = (cos(E) - sat.eccentricity) / (1.0 - sat.eccentricity * cos(E));
    double sinNu = (sqrt(1.0 - sat.eccentricity * sat.eccentricity) * sin(E)) / (1.0 - sat.eccentricity * cos(E));
    double nu = atan(sinNu, cosNu);

    // 4. 궤도 중심 거리 (r) 및 궤도 평면 상의 2D 좌표 (Perifocal)
    // 장반경(a) 도출 = (MU / n^2)^(1/3)
    double a = pow(MU / (n * n), 1.0 / 3.0); 
    double r = a * (1.0 - sat.eccentricity * cos(E));
    
    double pX = r * cos(nu);
    double pY = r * sin(nu);

    // 5. 3D ECI 좌표계로 회전 변환 (오일러 각: RAAN, Inclination, Arg of Perigee 적용)
    double cosO = cos(sat.raan);        double sinO = sin(sat.raan);
    double cosW = cos(sat.argPerigee);  double sinW = sin(sat.argPerigee);
    double cosI = cos(sat.inclination); double sinI = sin(sat.inclination);

    double x = pX * (cosO * cosW - sinO * sinW * cosI) - pY * (cosO * sinW + sinO * cosW * cosI);
    double y = pX * (sinO * cosW + cosO * sinW * cosI) - pY * (sinO * sinW - cosO * cosW * cosI);
    double z = pX * (sinW * sinI) + pY * (cosW * sinI);

    // 6. Zero-Copy 베이킹 완료! (Vertex Shader가 읽을 수 있도록 SSBO에 기록)
    orbitPositions[idx] = dvec4(x, y, z, 1.0);
}