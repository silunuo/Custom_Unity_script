using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OrbitAnimation : MonoBehaviour
{
    [Header("配置")]
    public Transform mainIcon;           // 拖入 MainIcon
    public Transform[] satellites;       // 拖入 3个 Satellite
    public float radius = 150f;          // 最终飞出的半径
    public float rotateSpeed = 100f;     // 旋转速度 (度/秒)
    public float expandSpeed = 5f;       // 飞出/缩回的平滑速度

    [Header("弹跳动画")]
    public AnimationCurve bounceCurve = new AnimationCurve(
        new Keyframe(0, 1), 
        new Keyframe(0.2f, 1.2f), 
        new Keyframe(0.5f, 1)
    ); // 默认设置一个简单的曲线

    // 内部变量
    private bool isSelected = false;
    private float currentRadius = 0f;
    private float currentAngle = 0f;

    void Start()
    {
        // 初始化：把所有卫星放在中心
        foreach (var sat in satellites)
        {
            sat.localPosition = Vector3.zero;
        }
    }

    void Update()
    {
        HandleOrbit();
    }

    // 处理卫星的飞出与旋转
    void HandleOrbit()
    {
        // 1. 计算当前的动态半径 (插值 Lerp)
        float targetR = isSelected ? radius : 0f;
        // 使用 Lerp 实现平滑的展开和收缩
        currentRadius = Mathf.Lerp(currentRadius, targetR, Time.deltaTime * expandSpeed);

        // 2. 只有当半径明显大于0时才计算旋转，节省性能
        if (currentRadius > 0.1f)
        {
            // 持续增加角度
            currentAngle += rotateSpeed * Time.deltaTime;
            float angleStep = 360f / satellites.Length; // 均分角度 (120度)
            for (int i = 0; i < satellites.Length; i++)
            {
                // 计算每个卫星的独立角度
                float angle = currentAngle + (angleStep * i);
                // 极坐标转笛卡尔坐标 (核心数学公式)
                // Mathf.Deg2Rad 将度数转为弧度
                float rad = angle * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * currentRadius;
                float y = Mathf.Sin(rad) * currentRadius;
                // 应用位置
                satellites[i].localPosition = new Vector3(x, y, 0);
                // satellites[i].localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
        else
        {
            // 如果完全缩回去了，强制归零防止浮点漂移
            foreach (var sat in satellites) sat.localPosition = Vector3.zero;
        }
    }
    // 公共方法：绑定到按钮点击事件
    public void OnClickToggle()
    {
        isSelected = !isSelected;
        // 播放中心图标的弹跳动画
        StopAllCoroutines();
        StartCoroutine(BounceRoutine());
    }
    // 简单的曲线动画协程
    IEnumerator BounceRoutine()
    {
        float timer = 0;
        float duration = 0.5f; // 动画时长
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float scaleValue = bounceCurve.Evaluate(timer / duration);
            mainIcon.localScale = Vector3.one * scaleValue;
            yield return null;
        }
        mainIcon.localScale = Vector3.one; // 归位
    }
}