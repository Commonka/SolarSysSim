using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceCamera : MonoBehaviour
{
    public Vector3d position;          // км, реальная позиция камеры
    public double speedFactor = 0.8;   // скорость = расстояние до поверхности * factor (в секунду)
    public float lookSensitivity = 0.15f;

    float yaw, pitch;

    public void Tick(CelestialBody[] bodies)
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Поворот: правая кнопка мыши
        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        // Расстояние до ближайшей поверхности
        double minDist = double.MaxValue;
        foreach (var b in bodies)
        {
            double dist = (position - b.position).Magnitude - b.radius;
            if (dist < minDist) minDist = dist;
        }
        minDist = System.Math.Max(minDist, 1.0);

        double speed = minDist * speedFactor;
        if (kb.leftShiftKey.isPressed) speed *= 5;

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed) move += Vector3.back;
        if (kb.dKey.isPressed) move += Vector3.right;
        if (kb.aKey.isPressed) move += Vector3.left;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move += Vector3.down;

        if (move != Vector3.zero)
        {
            Vector3 dir = transform.rotation * move.normalized;
            position += new Vector3d(dir.x, dir.y, dir.z) * (speed * Time.unscaledDeltaTime);
        }
    }

    public void LookAt(Vector3d target)
    {
        Vector3d d = (target - position).Normalized;
        transform.rotation = Quaternion.LookRotation(d.ToVector3());
        Vector3 e = transform.rotation.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180 ? e.x - 360 : e.x;
    }
}