#version 410 core

// Final pixel color output
layout (location = 0) out vec4 FragColor;

// 3D normal vector from Tessellation Evaluation Shader
in vec3 vNormal;

// 2D Earth Satellite Map Texture
uniform sampler2D earthTexture;

uniform bool isWireframe;     // wireframe mode
uniform vec3 wireColor;      // wireframe Color

const float PI = 3.14159265359;

void main() {
    // Re-normalize the interpolated vector to ensure its length is exactly 1.0
    vec3 n = normalize(vNormal); 

    if(isWireframe)
    {
        FragColor = vec4(1.0, 1.0, 0.0, 1.0);
    }
    else
    {
        // Equirectangular Mapping: Convert 3D vector (x, y, z) to 2D UV coordinates (0.0 to 1.0)
        float u = 0.5 + atan(n.z, n.x) / (2.0 * PI);
        float v = 0.5 - asin(n.y) / PI;
        vec2 uv = vec2(u, v);

        // Mipmap Seam Tearing Prevention Logic
        // Calculate screen-space derivatives to detect sudden jumps in UV coordinates
        vec2 dx = dFdx(uv);
        vec2 dy = dFdy(uv);

        // If the U coordinate jumps from 1.0 to 0.0 (or vice versa), correct the derivative
        if(dx.x > 0.5) dx.x -= 1.0;
        if(dx.x < -0.5) dx.x += 1.0;
        if(dy.x > 0.5) dy.x -= 1.0;
        if(dy.x < -0.5) dy.x += 1.0;

        // Sample the texture using the corrected derivatives to fix the seam
        FragColor = textureGrad(earthTexture, uv, dx, dy);
    }   
}