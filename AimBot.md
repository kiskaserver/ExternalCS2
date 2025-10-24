# AimBot — Detailed Design & Operation

This document explains how the project's AimBot works, where the code lives, key algorithms and data flows, and practical guidance for tuning and troubleshooting. It is written to complement `CONFIG_REFERENCE.md` and the code in `Features/AimBot.cs` and `Core/AimTrainer.cs`.

---

Summary / Contract
-----------------
- Inputs: Game memory (entity positions, bones, player eye position & direction, FOV), raw mouse input (global hook), configuration from config.json.
- Outputs: Synthetic mouse motion and optional synthetic left-clicks (when aimBotAutoShoot is enabled).
- Success criteria: Reduce aim error within human-like bounds while respecting user input and avoiding obviously non-human behavior.

---

Files & Code References
-----------------------
- Main implementation: Features/AimBot.cs (class AimBot)
- Statistical trainer: Core/AimTrainer.cs (class AimTrainer)
- Model files written at runtime:
  - Neural model: aim_model.pth (TorchSharp model saved/loaded by NeuralAimNetwork)
  - Statistical trainer storage: aim_trainer.json (written by AimTrainer.Save() in app base directory)

---

High-Level Algorithm (Per Frame / Tick)
---------------------------------------
1. Input gating
   - Runs only when manual hotkey (aimBotKey) is held or aimBotAutoShoot is enabled.
   - Suppresses bot movement briefly if recent raw mouse movement exceeds HumanReactThreshold (SuppressMs).

2. Calibration
   - Computes angles-per-pixel using sampled mouse moves and game aim direction changes (CalibrationMeasureHorizontalAnglePerPixel, CalibrationMeasureVerticalAnglePerPixel).
   - Sets _anglePerPixelHorizontal and _anglePerPixelVertical for pixel conversion.

3. Target selection & prediction
   - Iterates over visible enemies, examining prioritized bones (head, neck, chest, pelvis).
   - Computes small dynamic prediction using target velocity and distance.
   - Selects target with minimal angular size within configured FOV.

4. Correction calculation
   - Converts angular move to pixel offset (GetAimPixels).
   - Stacked corrections:
     - Statistical correction from AimTrainer.GetCorrection(distance)
     - Neural network correction from NeuralAimNetwork (input: 6 floats, output: 2 floats, 3 hidden layers × 64 units, outputs scaled via tanh × 5.0)
   - Sum applied to pixel offsets.

5. Humanization & smoothing
   - ApplyHumanizedAimAdjustments: scales, eases, adds micro-jitter.
   - _dynamicFov & _dynamicSmoothing adapt to recent success rate.
   - _aiAggressiveness adapts based on recent user input.

6. Movement and shooting
   - Pixel offsets passed to Utility.WindMouseMove(...) for smooth movement.
   - aimBotAutoShoot triggers synthetic mouse events (TryMouseDown()), respecting rate limits.

7. Training & observations
   - Residual pixel errors recorded into AimTrainer.AddObservation(distance, residualX, residualY).
   - Neural network trained online from recent samples in _trainingData.
   - Training lightweight; batch ~100 samples; uses MSE loss + Adam optimizer.

---

Key Classes & Methods
--------------------
- AimBot — constructor: sets trainer, neural network, hotkeys.
- FrameAction() — main per-frame logic: gating, calibration, target selection, corrections, humanization, mouse movement, shooting, training.
- GetAimTargetWithPrediction(double customFov) — selects target with dynamic prediction.
- GetAimAngles(...), GetAimPixels(...) — angular ↔ pixel transforms.
- ApplyHumanizedAimAdjustments(ref Point aimPixels, bool hasTarget) — easing, jitter, scaling.
- TrainNeuralNetwork() — forms tensors, runs forward/backward + optimizer step.
- Calibrate() and CalibrationMeasure* — compute angles-per-pixel.

---

Design Rationale & Safety Measures
---------------------------------
- User-first suppression: avoids fighting user input.
- Rate-limited shooting and conservative smoothing prevent robotic behavior.
- Statistical trainer: smooths residual biases.
- Neural network: handles complex corrections.
- Lightweight training: avoids runaway CPU usage.

---

Practical Tuning Knobs
----------------------
- HumanReactThreshold (AimBot.cs) — increase to reduce sensitivity to user movement.
- SuppressMs — suspension window after large input.
- _dynamicFov & _dynamicSmoothing — auto-adjusted, initial/clamp values in AimBot.cs.
- AimTrainer bucket size & MaxBucketCount — see Core/AimTrainer.cs.
- Neural network hyperparameters — hidden size, learning rate, batch/window size (InitNeuralNetwork()/TrainNeuralNetwork()).

---

Files to Manage / Troubleshooting
---------------------------------
- aim_model.pth — delete to reset neural model.
- aim_trainer.json — delete to reset statistical corrections.
- Logs — training errors and important events printed to console.

---

Edge Cases & Limitations
------------------------
- External reading only: outdated DTOs may yield incorrect memory.
- Performance: CPU-only TorchSharp is slower; consider CUDA.
- Safety & ethics: research-only; not for live multiplayer use.

---

Mathematical Details
-------------------
Vector definitions:
- Player eye: p = (px, py, pz)
- Target world: t = (tx, ty, tz)
- Player aim (unit vector): a
- Desired aim (unit vector): ad = (t - p)/||t - p||

Angle between vectors:
theta = arccos((a · ad) / (||a|| * ||ad||))

Yaw & pitch:
- yaw(v) = arctan2(vy, vx)
- pitch(v) = arcsin(-vz)

Convert angular correction → pixels:
pixels_x = theta_x / alpha_x * R
pixels_y = theta_y / alpha_y * R
with R = F_ref / F, angles-per-pixel alpha_x, alpha_y

Residuals & AimTrainer:
residualPixels_x = delta_phi / alpha_x
residualPixels_y = delta_psi / alpha_y
Buckets by distance k = round(distance / 100), mean corrections:
mu_x,k = S_x,k / n_k
mu_y,k = S_y,k / n_k

Neural Network:
- Input x in R^6 = [tx, ty, tz, px, py, pz]
- Output y in R^2 = (delta_x, delta_y)
- Architecture: 6 → Linear64 → ReLU → Linear64 → ReLU → Linear64 → ReLU → Linear2 → Tanh*5
- Loss: MSE, optimizer: Adam (η=0.001)

---

Diagrams
--------
Mermaid:

flowchart TD
   Game["CS2 Game Memory"] -->|Entity & Bone Positions| Detector["Entity Detector / GameData"]
   Detector --> Selector["Target Selector (GetAimTargetWithPrediction)"]
   Selector --> Correct["Correction Stack"]
   Correct -->|stat correction| AimTrainer["AimTrainer (mean residuals)"]
   Correct -->|neural correction| Neural[NeuralAimNetwork]
   Correct --> Combiner["Sum corrections"]
   Combiner --> Humanize["Humanization & Smoothing"]
   Humanize --> MouseMover["MouseMover (WindMouseMove)"]
   MouseMover --> OS["Operating System Mouse Events"]
   MouseMover -->|post-shot obs| TrainerLog["Observations -> AimTrainer"]
   TrainerLog --> AimTrainer
   TrainerLog -->|training buffer| NNTrain["NN Training (TrainNeuralNetwork)"]
   NNTrain --> Neural

ASCII fallback: GameMemory -> Detector -> Selector -> [AimTrainer + NeuralNet] -> Combiner -> Humanize -> MouseMover -> OS
Network: Input(6) -> [Linear64] -> ReLU -> [Linear64] -> ReLU -> [Linear64] -> ReLU -> [Linear2] -> Tanh*5

---

Suggested Improvements (Future Work)
-----------------------------------
- Normalize neural network inputs: subtract player position, divide by scale (e.g., units/1000)
- Add time/delta features: target velocity or previous corrections
- Move NN training to background thread or reduce frequency to bound CPU usage
- Optional L2 regularization or gradient clipping to avoid large weight updates

Example Math Walkthrough
------------------------
Assume:
- player FOV F = 90 (R = 1)
- horizontal angle per pixel alpha_x = 0.001 radians/pixel
- desired horizontal angle difference theta_x = 0.02 radians

Raw pixel offset:
pixels_x = 0.02 / 0.001 * 1 = 20 pixels

If neural predicts (-1.5, 0.7) and AimTrainer bucket mean is (-0.5, 0.0), total correction = (-2.0, 0.7), final pixel target = (18, 0.7) (rounded in code)