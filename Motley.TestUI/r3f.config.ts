/**
 * Centralized R3F / Three.js constants.
 *
 * Keeping magic numbers in one place makes tuning easier and prevents
 * inconsistencies across scenes (tone mapping, physics, camera, etc.).
 */

/* ------------------------------------------------------------------ */
/*  Physics (Rapier)                                                  */
/* ------------------------------------------------------------------ */

export const PHYSICS = {
  GRAVITY: [0, -16, 0] as [number, number, number],
  TIME_STEP: 1 / 60,
} as const

export const VEHICLE = {
  THRUST: 26,
  BRAKE: 18,
  REVERSE: 9,
  STEER_TORQUE: 5.2,
  STEER_SPEED_SCALE: 0.11,
  MAX_SPEED: 56,
  HUD_INTERVAL: 0.12,
  MASS: 2.6,
  LINEAR_DAMPING: 0.38,
  ANGULAR_DAMPING: 0.42,
  FRICTION: 0.92,
  RESTITUTION: 0.02,
} as const

/* ------------------------------------------------------------------ */
/*  Camera                                                            */
/* ------------------------------------------------------------------ */

export const BALATRO_CAMERA = {
  position: [0, 5, 8] as [number, number, number],
  fov: 50,
} as const

export const ADVENTURE_CAMERA = {
  position: [0, 2.35, 1.15] as [number, number, number],
  fov: 58,
  near: 0.1,
  far: 500,
} as const

/* ------------------------------------------------------------------ */
/*  Card animation (Balatro magnetic tilt)                            */
/* ------------------------------------------------------------------ */

export const CARD_MAGNET = {
  MAX_TILT_X: 0.36,
  MAX_TILT_Y: 0.42,
  MAX_SHIFT: 0.038,
  TWIST_Z: 0.11,
  LERP_IN: 18,
  LERP_OUT: 10,
} as const

export const CARD_DIMENSIONS = {
  WIDTH: 0.7,
  HEIGHT: 0.95,
  DEPTH: 0.02,
} as const

/* ------------------------------------------------------------------ */
/*  Billboard (HandDisplay Y-axis billboard lerp)                     */
/* ------------------------------------------------------------------ */

export const HAND_BILLBOARD_LERP = 3

/* ------------------------------------------------------------------ */
/*  Renderer                                                          */
/* ------------------------------------------------------------------ */

/** Cap DPR to avoid GPU-bound frames on high-density mobile screens. */
export const MAX_DPR = 2
