#version 430 core

// Input from C# (64-bit precision 3D coordinates of the cube)
layout (location = 0) in dvec3 aPos;

// Output to Tessellation Control Shader
out dvec3 vPos;

void main() {
    // Pass the original vertex coordinates directly to TCS without transformation
    vPos = aPos;
}