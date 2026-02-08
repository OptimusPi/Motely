<template>
  <canvas ref="canvasRef" class="balatro-shader-background" />
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const canvasRef = ref(null)
let animationFrameId = null
let gl = null
let program = null
let timeLocation = null
let spinTimeLocation = null
let resolutionLocation = null

// Balatro shader colors
const COLOR1 = [254/255, 95/255, 85/255]  // #FE5F55 Red
const COLOR2 = [0/255, 157/255, 255/255]   // #009dff Blue  
const COLOR3 = [55/255, 66/255, 68/255]    // #374244 Black

const vertexShaderSource = `
  attribute vec2 a_position;
  void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
  }
`

const fragmentShaderSource = `
  precision mediump float;
  
  uniform float u_time;
  uniform float u_spinTime;
  uniform vec2 u_resolution;
  
  vec3 color1 = vec3(${COLOR1[0]}, ${COLOR1[1]}, ${COLOR1[2]});
  vec3 color2 = vec3(${COLOR2[0]}, ${COLOR2[1]}, ${COLOR2[2]});
  vec3 color3 = vec3(${COLOR3[0]}, ${COLOR3[1]}, ${COLOR3[2]});
  
  float contrast = 1.8;
  float spinAmount = 0.6;
  float spinEase = 0.7;
  float pixelSize = 128.0;
  float parallaxX = -0.01;
  int loopCount = 3;
  
  vec3 calculatePixel(vec2 coord, float time, float spinTime) {
    float resolution = length(u_resolution);
    float pixSize = resolution / pixelSize;
    
    vec2 uv = (floor(coord / pixSize) * pixSize - 0.5 * u_resolution) / resolution;
    uv.x -= parallaxX;
    float uvLen = length(uv);
    
    float speed = spinTime * spinEase * 0.2 + 302.2;
    float newAngle = atan(uv.y, uv.x) + speed - spinEase * 20.0 * (spinAmount * uvLen + (1.0 - spinAmount));
    
    uv = vec2(uvLen * cos(newAngle), uvLen * sin(newAngle));
    uv *= 30.0;
    
    float animSpeed = time * 2.0;
    vec2 uv2 = vec2(uv.x + uv.y, uv.x + uv.y);
    
    for(int i = 0; i < 3; i++) {
      float maxUv = max(uv.x, uv.y);
      uv2.x += sin(maxUv) + uv.x;
      uv2.y += sin(maxUv) + uv.y;
      uv.x += 0.5 * cos(5.1123314 + 0.353 * uv2.y + animSpeed * 0.131121);
      uv.y += 0.5 * sin(uv2.x - 0.113 * animSpeed);
      float cosVal = cos(uv.x + uv.y);
      float sinVal = sin(uv.x * 0.711 - uv.y);
      uv.x -= cosVal - sinVal;
      uv.y -= cosVal - sinVal;
    }
    
    float contrastMod = 0.25 * contrast + 0.5 * spinAmount + 1.2;
    float paintRes = length(uv) * 0.035 * contrastMod;
    paintRes = clamp(paintRes, 0.0, 2.0);
    
    float c1p = max(0.0, 1.0 - contrastMod * abs(1.0 - paintRes));
    float c2p = max(0.0, 1.0 - contrastMod * abs(paintRes));
    float c3p = 1.0 - min(1.0, c1p + c2p);
    
    float cf = 0.3 / contrast;
    float ncf = 1.0 - cf;
    
    vec3 color = cf * color1 + ncf * (color1 * c1p + color2 * c2p + color3 * c3p);
    return color;
  }
  
  void main() {
    vec2 coord = gl_FragCoord.xy;
    vec3 color = calculatePixel(coord, u_time, u_spinTime);
    gl_FragColor = vec4(color, 1.0);
  }
`

function createShader(gl, type, source) {
  const shader = gl.createShader(type)
  gl.shaderSource(shader, source)
  gl.compileShader(shader)
  
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error('Shader compilation error:', gl.getShaderInfoLog(shader))
    gl.deleteShader(shader)
    return null
  }
  
  return shader
}

function initWebGL() {
  const canvas = canvasRef.value
  if (!canvas) return false
  
  gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl')
  if (!gl) {
    console.error('WebGL not supported')
    return false
  }
  
  // Create shaders
  const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexShaderSource)
  const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentShaderSource)
  
  if (!vertexShader || !fragmentShader) return false
  
  // Create program
  program = gl.createProgram()
  gl.attachShader(program, vertexShader)
  gl.attachShader(program, fragmentShader)
  gl.linkProgram(program)
  
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    console.error('Program linking error:', gl.getProgramInfoLog(program))
    return false
  }
  
  // Set up geometry
  const positions = new Float32Array([
    -1, -1,
     1, -1,
    -1,  1,
     1,  1,
  ])
  
  const positionBuffer = gl.createBuffer()
  gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer)
  gl.bufferData(gl.ARRAY_BUFFER, positions, gl.STATIC_DRAW)
  
  const positionLocation = gl.getAttribLocation(program, 'a_position')
  gl.enableVertexAttribArray(positionLocation)
  gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0)
  
  // Get uniform locations
  timeLocation = gl.getUniformLocation(program, 'u_time')
  spinTimeLocation = gl.getUniformLocation(program, 'u_spinTime')
  resolutionLocation = gl.getUniformLocation(program, 'u_resolution')
  
  return true
}

function resizeCanvas() {
  const canvas = canvasRef.value
  if (!canvas || !gl) return
  
  canvas.width = canvas.offsetWidth
  canvas.height = canvas.offsetHeight
  
  if (gl) {
    gl.viewport(0, 0, canvas.width, canvas.height)
    if (resolutionLocation) {
      gl.uniform2f(resolutionLocation, canvas.width, canvas.height)
    }
  }
}

let startTime = Date.now()
function render() {
  if (!gl || !program) return
  
  const currentTime = (Date.now() - startTime) / 1000
  
  gl.useProgram(program)
  
  // Update uniforms
  if (timeLocation) gl.uniform1f(timeLocation, currentTime)
  if (spinTimeLocation) gl.uniform1f(spinTimeLocation, currentTime * 0.75)
  if (resolutionLocation) gl.uniform2f(resolutionLocation, canvasRef.value.width, canvasRef.value.height)
  
  // Draw
  gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4)
  
  animationFrameId = requestAnimationFrame(render)
}

onMounted(() => {
  if (initWebGL()) {
    resizeCanvas()
    render()
    
    window.addEventListener('resize', resizeCanvas)
  }
})

onUnmounted(() => {
  if (animationFrameId) {
    cancelAnimationFrame(animationFrameId)
  }
  window.removeEventListener('resize', resizeCanvas)
  
  if (gl) {
    gl.deleteProgram(program)
  }
})
</script>

<style scoped>
.balatro-shader-background {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  opacity: 0.8;
}
</style>
