using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIController : Controller
{
    public enum eAlarmState { None, Cautious, Alarmed };

    [SerializeField] private Sprite[] m_AlarmSprites;

    [SerializeField] private SpriteRenderer m_AlarmIcon;
    [SerializeField] private float m_ChaseSpeed = 5f;
    [SerializeField] private float m_MaxLookDistance = 10f;
    [SerializeField] private LayerMask m_SuspicionMask;
    [SerializeField] private float m_TimeToCaution = 2f;
    [SerializeField] private float m_TimeToAlarm = 1f;

    private eAlarmState m_PreviousState = eAlarmState.Cautious;
    private eAlarmState m_AlarmState = eAlarmState.None;
    private Transform m_Target;
    private Vector2 m_TargetPosition;

    private float m_DecisionTime = 0f;
    private float m_WaitTime = 0f;
    private float m_Time = 0f;

    RaycastHit2D m_HitInfo;
    private float m_GeneralAlarmTime = 0f;

    private void Start()
    {
        ChangeState(eAlarmState.None);
    }

    private void ChangeState(eAlarmState state)
    {
        // set icon
        if (m_PreviousState != m_AlarmState)
        {
            m_AlarmIcon.sprite = m_AlarmSprites[(int)m_AlarmState];
        }

        m_PreviousState = m_AlarmState;
        m_AlarmState = state;
    }

    private void FixedUpdate()
    {
        switch (m_AlarmState)
        {
            case eAlarmState.None:
                // wander
                if (m_SurfaceBelow)
                {

                    // THIS ISN'T RIGHT. FIX IT PLEASE.

                    if (Time.time < m_Time + m_WaitTime)
                    {
                        Debug.Log("Waiting...");
                        return;
                    }

                    if (Time.time > m_Time + m_DecisionTime)
                    {
                        m_Time = Time.time;
                        m_DecisionTime = UnityEngine.Random.Range(1f, 3f);

                        float direction = (UnityEngine.Random.Range(-1f, 1f) < 0f ? -1 : 1);
                        m_MovementX = direction * m_DefaultMoveSpeed;

                        Debug.LogFormat("decision time: {0}, wait time: {1}", m_DecisionTime, m_WaitTime);
                    }

                    Vector2 velocity = m_Rigidbody.velocity;
                    float smoothTime = m_AccelerationGrounded;
                    velocity.x = Mathf.SmoothDamp(velocity.x, m_MovementX, ref m_VelXSmooth, smoothTime);
                    Move(velocity);

                    // look for suspicious things
                    m_HitInfo = Physics2D.Raycast(m_CachedTransform.position, m_CachedTransform.right * Mathf.Sign(m_MovementX), m_MaxLookDistance);
                    if (m_HitInfo.collider != null && m_HitInfo.collider != m_Collider && CollisionHandler.LayerInLayerMask(m_HitInfo.collider.gameObject.layer, m_SuspicionMask))
                    {
                        Debug.Log(m_HitInfo.collider.name);
                        m_GeneralAlarmTime += Time.fixedDeltaTime;
                        if (m_GeneralAlarmTime > m_TimeToCaution)
                        {
                            m_GeneralAlarmTime = 0f;
                            m_TargetPosition = m_HitInfo.collider.transform.position;
                            ChangeState(eAlarmState.Cautious);
                        }
                    }
                }
                break;

            case eAlarmState.Cautious:
                // walk towards the last known location of the player
                // if the player remains in sight for more than x seconds, become alarmed
                // if the player goes out of sight, and is out for more than y seconds, return to none state
                break;

            case eAlarmState.Alarmed:
                // lock the player as the 'target'
                // follow the player as much as possible
                // shoot at them (TODO)
                // if the player goes out of sight or is in a place that you can't get to for more than z seconds, return to cautious
                break;
        }
    }
}
