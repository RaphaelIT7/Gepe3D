#version 330

uniform vec3 lightPos;
uniform float sphereRadius;

out vec4 FragColor;

in vec3 fragNormal;
in vec3 fragPos;
in vec4 instanceAlbedo;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

void main()
{
    float d2 = fragPos.x * fragPos.x + fragPos.y * fragPos.y;
    
    if (d2 > 1) discard;
    
    // view space
    vec4 vLightPos = viewMatrix * vec4(lightPos, 1.0);
    vec4 vFragPos = viewMatrix * vec4(fragPos, 1.0);
    vec3 lightDir = normalize( vLightPos.xyz - vFragPos.xyz );
    
    float nx = fragPos.x;
    float ny = fragPos.y;
    float nz = sqrt(1.0 - d2);
    vec3 normal = vec3(nx, ny, nz);
    
    float NdL = max( 0.0, dot(normal, lightDir) );
    FragColor = vec4(instanceAlbedo.xyz * NdL, 1);
    
    // clip space
    vec4 cFragPos = projectionMatrix * vec4( vFragPos.xyz + normal * sphereRadius, 1 );
    // gl_FragDepth = cFragPos.z / cFragPos.w;
}