using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private Transform m_ModelContainer;
    [SerializeField] private Transform m_AimTargetTransform;
    [SerializeField] private GameObject m_ArrowPrefab;
    [SerializeField] private Sack m_Sack;

    [SerializeField] private float m_DefaultMoveSpeed = 5f;
    [SerializeField] private float m_CrouchedMoveSpeed = 3f;
    [SerializeField] private float m_MaxJumpVelocity = 6f;
    [SerializeField] private float m_MinJumpVelocity = 2f;
    [SerializeField] private float m_AccelerationGrounded = 0.5f;
    [SerializeField] private float m_AccelerationAirborne = 0.75f;
    [SerializeField] private float m_WallSlideVelocity = -1f;
    [SerializeField] private float m_ShootForce = 5f;
    [SerializeField] private int m_MaxGrappleCount = 1;

    private bool m_TouchingJumpableSurface { get { return m_SurfaceBelow || m_SurfaceLeft || m_SurfaceRight; } }

    private float m_Movement;
    private float m_VelXSmooth;
    private bool m_RequestJump = false;
    private bool m_CancelJump = false;
    private bool m_Crouched = false;

    private bool m_SurfaceBelow = false;
    private bool m_SurfaceLeft = false;
    private bool m_SurfaceRight = false;
    private bool m_SurfaceAbove = false;

    private bool m_Aiming = false;
    private bool m_Grappling = false;
    private Vector3 m_TargetPos = Vector3.zero;

    private int m_TotalGrapples = 0;
    private List<GameObject> m_PrevGrappleArrows;
    private List<DistanceJoint2D> m_GrappleJoints;
    private List<LineRenderer> m_GrappleRenderers;

    private Material m_GrappleMat;

    private void Awake()
    {
        InputManager.Instance.AddInputEventDelegate(OnInputRecieved, UpdateLoopType.Update);

        m_GrappleMat = new Material(Shader.Find("Sprites/Default"));

        m_PrevGrappleArrows = new List<GameObject>();
        m_GrappleJoints = new List<DistanceJoint2D>();
        m_GrappleRenderers = new List<LineRenderer>();
    }

    private void OnInputRecieved(InputActionEventData data)
    {
        switch (data.actionId)
        {
            // move
            case RewiredConsts.Action.Move_Horizontal:
                m_Movement = 0f;

                float h = data.GetAxis();
                if (h != 0f)
                {
                    m_Movement = h * (m_Crouched ? m_CrouchedMoveSpeed : m_DefaultMoveSpeed);
                }
                break;

            // shoot/grapple
            case RewiredConsts.Action.Shoot: // fall through
            case RewiredConsts.Action.Grapple:
                if (data.GetButton())
                {
                    m_Aiming = true;
                }
                else if (data.GetButtonUp())
                {
                    ShowAimTarget(false);
                    m_Aiming = false;

                    Shoot(data.actionId == RewiredConsts.Action.Grapple);
                }
                break;

            // aim
            case RewiredConsts.Action.Aim_Horizontal:
                m_TargetPos.x = 0f;

                float aH = data.GetAxis();
                if (aH != 0f)
                {
                    m_TargetPos.x = aH;
                }
                break;

            case RewiredConsts.Action.Aim_Vertical:
                m_TargetPos.y = 0f;

                float aV = data.GetAxis();
                if (aV != 0f)
                {
                    m_TargetPos.y = aV;
                }
                break;

            // jump
            case RewiredConsts.Action.Jump:
                if (data.GetButtonDown() && (m_TouchingJumpableSurface || m_Grappling))
                {
                    m_RequestJump = true;
                }
                else if (data.GetButtonUp())
                {
                    m_CancelJump = true;
                }
                break;

            // crouch
            case RewiredConsts.Action.Crouch:
                if (data.GetButtonDown() && m_SurfaceBelow)
                {
                    m_Crouched = !m_Crouched;
                    Debug.LogFormat("Crouched: {0}", m_Crouched);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
        CancelJump();
        Aim();

        if (m_Grappling)
        {
            for (int i = 0; i < m_GrappleRenderers.Count; i++)
            {
                m_GrappleRenderers[i].SetPosition(1, m_CachedTransform.position);
            }
        }
        else if (!m_SurfaceBelow)
        {
            // you're not grappling and in the air, attempt to right the player
            m_Rigidbody.MoveRotation(Mathf.Lerp(m_Rigidbody.rotation, 0f, 0.2f));
        }
    }

    private void Move()
    {
        if (m_Rigidbody != null && !m_Grappling)
        {
            Vector2 velocity = m_Rigidbody.velocity;
            
            // block moving into a wall
            float direction = Mathf.Sign(m_Movement);
            if ((direction < 0f && !m_SurfaceLeft) || (direction > 0f && !m_SurfaceRight))
            {
                float smoothTime = (m_TouchingJumpableSurface ? m_AccelerationGrounded : m_AccelerationAirborne);
                velocity.x = Mathf.SmoothDamp(velocity.x, m_Movement, ref m_VelXSmooth, smoothTime);
            }
            else
            {
                // sliding down wall
                velocity.y = m_WallSlideVelocity;
            }

            FlipSprite(direction);
            m_Rigidbody.velocity = velocity;
        }
    }

    private void FlipSprite(float direction)
    {
        Vector3 localScale = m_ModelContainer.localScale;
        localScale.x = (direction < 0f ? -1 : 1);
        m_ModelContainer.localScale = localScale;
    }

    private void Jump()
    {
        if ((m_TouchingJumpableSurface || m_Grappling) && m_RequestJump)
        {
            if (m_Grappling)
            {
                CancelGrapple();
            }

            Vector2 velocity = m_Rigidbody.velocity;

            // account for sack weight
            float deltaVelocity = m_MaxJumpVelocity - m_MinJumpVelocity;
            float weightedVelocity = m_MaxJumpVelocity - (m_Sack.WeightPercent * deltaVelocity);

            velocity.y = weightedVelocity;

            Debug.LogFormat("weight %: {0}, weighted vel: {1}, min vel: {2}, max vel: {3}", m_Sack.WeightPercent, weightedVelocity, m_MinJumpVelocity, m_MaxJumpVelocity);

            m_Rigidbody.velocity = velocity;
            m_RequestJump = false;
            m_Crouched = false;
        }
    }

    private void CancelJump()
    {
        if (m_CancelJump)
        {
            Vector2 velocity = m_Rigidbody.velocity;
            if (velocity.y > m_MinJumpVelocity)
            {
                velocity.y = m_MinJumpVelocity;

                m_Rigidbody.velocity = velocity;
            }
            m_CancelJump = false;
        }
    }

    private void Aim()
    {
        if (m_Aiming)
        {
            if (m_TargetPos != Vector3.zero)
            {
                ShowAimTarget(true);
                m_AimTargetTransform.localPosition = m_TargetPos;
            }
            else
            {
                ShowAimTarget(false);
            }
        }
    }

    private void Shoot(bool grapple)
    {
        GameObject arrowObj = (GameObject)Instantiate(m_ArrowPrefab, null);
        arrowObj.transform.position = m_AimTargetTransform.position;
        m_PrevGrappleArrows.Add(arrowObj);

        Arrow arrow = arrowObj.GetComponent<Arrow>();
        if (arrow != null)
        {
            System.Action<Rigidbody2D> callback = null;
            if (grapple)
            {
                callback = OnGrappleArrowLanded;
            }

            Vector3 direction = (m_AimTargetTransform.position - m_CachedTransform.position).normalized;
            arrow.Launch(direction, m_ShootForce, callback);

            // push player back
            m_Rigidbody.AddForce(-direction * 2f, ForceMode2D.Impulse);
        }
    }

    private void OnGrappleArrowLanded(Rigidbody2D arrowRb)
    {
        if (m_Grappling)
        {
            CancelGrapple();
        }

        //Vector3 arrowPos = arrowRb.transform.position;
        //m_GrappleJoint.distance = (arrowPos - m_CachedTransform.position).magnitude;
        //m_GrappleJoint.connectedBody = arrowRb;
        //m_GrappleJoint.enabled = true;

        //// set static end of line
        //m_GrappleRenderer.enabled = true;
        //m_GrappleRenderer.positionCount = 2;
        //m_GrappleRenderer.SetPosition(0, arrowPos);
        //m_GrappleRenderer.SetPosition(1, m_CachedTransform.position);

        if (m_TotalGrapples >= m_MaxGrappleCount)
        {
            CancelGrapple();
        }

        BuildGrapple(arrowRb);
        m_TotalGrapples += 1;

        Debug.LogFormat("Adding Grapple: {0}", m_TotalGrapples);

        m_Grappling = true;
    }

    private void BuildGrapple(Rigidbody2D connectedBody)
    {
        // build joint
        DistanceJoint2D joint = this.gameObject.AddComponent<DistanceJoint2D>();
        joint.enableCollision = true;
        joint.maxDistanceOnly = true;

        joint.distance = (connectedBody.transform.position - m_CachedTransform.position).magnitude;
        joint.connectedBody = connectedBody;
        joint.enabled = true;

        m_GrappleJoints.Add(joint);

        // build line renderer
        LineRenderer renderer = this.gameObject.AddComponent<LineRenderer>();
        renderer.material = m_GrappleMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.useWorldSpace = true;

        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0, 0.1f);
        widthCurve.AddKey(1, 0.1f);
        renderer.widthCurve = widthCurve;

        renderer.startColor = new Color(0.486f, 0.192f, 0.192f, 1f);
        renderer.endColor = new Color(0.486f, 0.192f, 0.192f, 1f);

        renderer.enabled = true;
        renderer.positionCount = 2;
        renderer.SetPosition(0, connectedBody.transform.position);
        renderer.SetPosition(1, m_CachedTransform.position);

        m_GrappleRenderers.Add(renderer);
    }

    private void CancelGrapple()
    {
        if (m_PrevGrappleArrows.Count > 0)
        {
            Destroy(m_PrevGrappleArrows[0], 5f);
            Destroy(m_GrappleJoints[0]);
            Destroy(m_GrappleRenderers[0]);

            m_PrevGrappleArrows.RemoveAt(0);
            m_GrappleJoints.RemoveAt(0);
            m_GrappleRenderers.RemoveAt(0);

            m_TotalGrapples -= 1;

            Debug.LogFormat("Removing Grapple: {0}", m_TotalGrapples);
        }

        //m_GrappleJoint.connectedBody = null;
        //m_GrappleJoint.enabled = false;

        m_Grappling = false;
        //m_GrappleRenderer.enabled = false;
    }

    private void ShowAimTarget(bool show)
    {
        m_AimTargetTransform.gameObject.SetActive(show);
    }

    public void HandleTriggerCollision(eCollisionZone zone, bool enter)
    {
        switch (zone)
        {
            case eCollisionZone.Below:
                m_SurfaceBelow = enter;
                break;

            case eCollisionZone.Above:
                m_SurfaceAbove = enter;
                break;

            case eCollisionZone.Left:
                m_SurfaceLeft = enter;
                break;

            case eCollisionZone.Right:
                m_SurfaceRight = enter;
                break;
        }

        //Debug.LogFormat("Zone: {0}, Enter: {1}", zone, enter);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.gameObject.layer == LayerMask.NameToLayer("Treasure"))
        {
            Treasure t = collider.gameObject.GetComponent<Treasure>();
            if (t != null)
            {
                int value = t.Value;
                bool accepted = m_Sack.Fill(value);
                if (accepted)
                {
                    Destroy(collider.gameObject);
                }
                else
                {
                    t.Reject();
                }
            }
            else
            {
                Debug.LogError("No treasure component found");
            }
        }
    }
}
