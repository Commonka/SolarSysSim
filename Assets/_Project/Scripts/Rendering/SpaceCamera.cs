using UnityEngine;
using UnityEngine.InputSystem;

public class SpaceCamera : MonoBehaviour
{
    public Vector3d position;          // км, реальная позиция камеры
    public double speedFactor = 0.8;   // скорость = расстояние до поверхности * factor (в секунду)
    public float lookSensitivity = 0.15f;

    [Header("Скорость (цифры 1-0, колёсико, R = сброс)")]
    public double speedMultiplier = 1.0;   // пользовательский множитель
    public float scrollStep = 1.25f;       // во сколько раз меняется за щелчок колёсика
    public double boostFactor = 5.0;       // ускорение на Ctrl

    // Клавиши 1..9, 0 -> множитель скорости
    static readonly double[] digitMultipliers = { 0.01, 0.03, 0.1, 0.3, 1, 3, 10, 30, 100, 1000 };

    float yaw, pitch;
    double currentSpeed; // км/с, для показа на экране

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

        // Цифры 1..9 -> индексы 0..8, цифра 0 -> индекс 9
        for (int i = 0; i < 9; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame) speedMultiplier = digitMultipliers[i];
        if (kb.digit0Key.wasPressedThisFrame) speedMultiplier = digitMultipliers[9];

        // Колёсико мыши: плавная подстройка множителя
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            speedMultiplier *= System.Math.Pow(scrollStep, Mathf.Sign(scroll));
            speedMultiplier = System.Math.Max(0.001, System.Math.Min(1000.0, speedMultiplier));
        }
        if (kb.rKey.wasPressedThisFrame) speedMultiplier = 1.0;

        // Расстояние до ближайшей поверхности
        double minDist = double.MaxValue;
        foreach (var b in bodies)
        {
            double dist = (position - b.position).Magnitude - b.radius;
            if (dist < minDist) minDist = dist;
        }
        minDist = System.Math.Max(minDist, 1.0);

        double speed = minDist * speedFactor * speedMultiplier;
        if (kb.leftCtrlKey.isPressed) speed *= boostFactor;
        currentSpeed = speed;

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed) move += Vector3.back;
        if (kb.dKey.isPressed) move += Vector3.right;
        if (kb.aKey.isPressed) move += Vector3.left;
        if (kb.spaceKey.isPressed) move += Vector3.up;
        if (kb.leftShiftKey.isPressed) move += Vector3.down;

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

    void OnGUI()
    {
        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float virtualHeight = Screen.height / s;
        GUI.Label(new Rect(10, virtualHeight - 50, 700, 40),
            $"Скорость камеры: {currentSpeed:N0} км/с  (множитель ×{speedMultiplier:G3})\n" +
            "1-0: множитель, колёсико: подстройка, R: сброс, Ctrl: ×5, Пробел/Shift: вверх/вниз");
    }
}