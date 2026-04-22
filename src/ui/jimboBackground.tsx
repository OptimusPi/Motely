'use client'

import React, { useEffect, useRef } from 'react'

/**
 * Fullscreen WebGL CRT/spin background — the authentic Balatro hypnotic
 * swirl, pixelated and animated. Ported from weejoker.app's
 * BackgroundShader.tsx; no config required.
 *
 * Renders a fixed-position canvas at z-index: -10 that fills the viewport
 * and ignores pointer events. Drop it once at the root of your page:
 *
 *     <JimboBackground />
 *     <YourAppContent />
 *
 * Resizes automatically. Disposes the animation frame + shader on unmount.
 */
export function JimboBackground() {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const gl = canvas.getContext('webgl')
    if (!gl) return

    const vsSource = `
      attribute vec2 position;
      void main() {
        gl_Position = vec4(position, 0.0, 1.0);
      }
    `

    const fsSource = `
      precision mediump float;

      uniform float u_time;
      uniform vec2 u_resolution;

      const float SPIN_ROTATION = -2.0;
      const float SPIN_SPEED = 4.5;
      const vec4 COLOUR_1 = vec4(1.0, 0.2, 0.2, 1.0);
      const vec4 COLOUR_2 = vec4(0.0, 0.5, 1.0, 1.0);
      const vec4 COLOUR_3 = vec4(0.05, 0.08, 0.1, 1.0);
      const float CONTRAST = 4.5;
      const float LIGTHING = 0.5;
      const float SPIN_AMOUNT = 0.35;
      const float PIXEL_FILTER = 1024.0;
      const float PI = 3.14159265359;

      void main() {
        vec2 screenSize = u_resolution;
        float pixel_size = length(screenSize.xy) / PIXEL_FILTER;
        vec2 uv = (floor(gl_FragCoord.xy*(1.0/pixel_size))*pixel_size - 0.5*screenSize.xy)/length(screenSize.xy);
        float uv_len = length(uv);

        float speed = (SPIN_ROTATION * 0.2) + 302.2;
        float new_pixel_angle = atan(uv.y, uv.x) + speed - 20.0*(1.0*SPIN_AMOUNT*uv_len + (1.0 - 1.0*SPIN_AMOUNT));

        vec2 mid = (screenSize.xy/length(screenSize.xy))/2.0;
        uv = (vec2((uv_len * cos(new_pixel_angle) + mid.x), (uv_len * sin(new_pixel_angle) + mid.y)) - mid);

        uv *= 30.0;
        speed = u_time * SPIN_SPEED;
        vec2 uv2 = vec2(uv.x, uv.y);

        for(int i=0; i < 5; i++) {
          uv2 += sin(max(uv.x, uv.y)) + uv;
          uv  += 0.5*vec2(cos(5.1123314 + 0.353*uv2.y + speed*0.131121), sin(uv2.x - 0.113*speed));
          uv  -= 1.0*cos(uv.x + uv.y) - 1.0*sin(uv.x*0.711 - uv.y);
        }

        float contrast_mod = (0.25*CONTRAST + 0.5*SPIN_AMOUNT + 1.2);
        float paint_res = min(2.0, max(0.0, length(uv)*(0.035)*contrast_mod));
        float c1p = max(0.0, 1.0 - contrast_mod*abs(1.0 - paint_res));
        float c2p = max(0.0, 1.0 - contrast_mod*abs(paint_res));
        float c3p = 1.0 - min(1.0, c1p + c2p);
        float light = (LIGTHING - 0.2)*max(c1p*5.0 - 4.0, 0.0) + LIGTHING*max(c2p*5.0 - 4.0, 0.0);

        vec4 finalCol = (0.3/CONTRAST)*COLOUR_1 + (1.0 - 0.3/CONTRAST)*(COLOUR_1*c1p + COLOUR_2*c2p + vec4(c3p*COLOUR_3.rgb, c3p*COLOUR_1.a)) + light;

        gl_FragColor = finalCol;
      }
    `

    const createShader = (type: number, source: string) => {
      const shader = gl.createShader(type)
      if (!shader) return null
      gl.shaderSource(shader, source)
      gl.compileShader(shader)
      if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        console.error('[JimboBackground] shader compile error:', gl.getShaderInfoLog(shader))
        gl.deleteShader(shader)
        return null
      }
      return shader
    }

    const vertexShader = createShader(gl.VERTEX_SHADER, vsSource)
    const fragmentShader = createShader(gl.FRAGMENT_SHADER, fsSource)
    if (!vertexShader || !fragmentShader) return

    const program = gl.createProgram()
    if (!program) return
    gl.attachShader(program, vertexShader)
    gl.attachShader(program, fragmentShader)
    gl.linkProgram(program)
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      console.error('[JimboBackground] program link error:', gl.getProgramInfoLog(program))
      return
    }

    gl.useProgram(program)

    const positionBuffer = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer)
    gl.bufferData(
      gl.ARRAY_BUFFER,
      new Float32Array([
        -1.0, -1.0,  1.0, -1.0, -1.0, 1.0,
        -1.0,  1.0,  1.0, -1.0,  1.0, 1.0,
      ]),
      gl.STATIC_DRAW,
    )

    const positionLocation = gl.getAttribLocation(program, 'position')
    gl.enableVertexAttribArray(positionLocation)
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0)

    const timeLocation = gl.getUniformLocation(program, 'u_time')
    const resolutionLocation = gl.getUniformLocation(program, 'u_resolution')

    const startTime = Date.now()
    let animationFrameId: number

    const render = () => {
      const displayWidth = canvas.clientWidth
      const displayHeight = canvas.clientHeight
      if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth
        canvas.height = displayHeight
        gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight)
      }

      const currentTime = (Date.now() - startTime) / 1000.0
      gl.uniform1f(timeLocation, currentTime)
      gl.uniform2f(resolutionLocation, canvas.width, canvas.height)

      gl.drawArrays(gl.TRIANGLES, 0, 6)
      animationFrameId = requestAnimationFrame(render)
    }

    render()

    return () => {
      cancelAnimationFrame(animationFrameId)
      gl.deleteProgram(program)
      gl.deleteShader(vertexShader)
      gl.deleteShader(fragmentShader)
      gl.deleteBuffer(positionBuffer)
    }
  }, [])

  return (
    <canvas
      ref={canvasRef}
      aria-hidden
      style={{
        position: 'fixed',
        inset: 0,
        width: '100%',
        height: '100%',
        zIndex: -10,
        pointerEvents: 'none',
      }}
    />
  )
}
