using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private TrailRenderer m_Trail;

    [SerializeField] private DistanceJoint2D m_RopeJoint;
    [SerializeField] private LineRenderer m_RopeRenderer;

    [SerializeField] private float m_RecordInterval = 0.25f;

    private bool m_Launched = false;
    private float m_PrevRotation;
    private Vector2 m_Velocity;
    private System.Action m_GrappleCallback;
    private Transform m_Owner;

    private float m_Time = 0f;
    private List<Vector3> m_ArrowPos = new List<Vector3>();
    private Vector3 m_OwnerToArrowAnchor;
    private int m_TotalPoints = 0;

    private void FixedUpdate()
    {
        if (m_Launched)
        {
            // update rotation based on velocity
            m_PrevRotation = m_Rigidbody.rotation;

            m_Velocity = m_Rigidbody.velocity;
            float angle = Mathf.Atan2(m_Velocity.y, m_Velocity.x) * Mathf.Rad2Deg;
            m_Rigidbody.MoveRotation(angle);

            // update rope
            if (m_GrappleCallback != null)
            {
                //m_RopeRenderer.positionCount = 2;
                //m_RopeRenderer.SetPosition(0, m_Owner.position);
                //m_RopeRenderer.SetPosition(1, m_CachedTransform.position);

                if (Time.time > m_Time + m_RecordInterval)
                {
                    m_Time = Time.time;
                    m_ArrowPos.Add(m_CachedTransform.position);
                }

                int count = m_ArrowPos.Count;
                int halfCount = count / 2;
                m_RopeRenderer.positionCount = count;

                for (int i = 0; i < count; i++)
                {
                    m_RopeRenderer.SetPosition(0, m_Owner.position);
                    if (i != 0 && i != count - 1)
                    {
                        Vector3 point = m_ArrowPos[i];
                        float directionX = 0f;
                        if (i < halfCount)
                        {
                            directionX = Mathf.Sign(m_Owner.position.x - point.x);
                        }
                        else
                        {
                            directionX = Mathf.Sign(m_CachedTransform.position.x - point.x);
                        }

                        // pull point towards direction
                        point.x += 0.025f * directionX;

                        if (point.y < m_Owner.position.y)
                        {
                            point.y += 0.075f;
                        }

                        // fake some rope gravity
                        point.y -= 0.05f;

                        m_ArrowPos[i] = point;
                        m_RopeRenderer.SetPosition(i, m_ArrowPos[i]);
                    }
                    m_RopeRenderer.SetPosition(count - 1, m_CachedTransform.position);
                }
            }
        }
        else
        {
            // pull rope taught
            m_OwnerToArrowAnchor = m_CachedTransform.position - m_Owner.position;

            // if the number of points is the count, all points are in line and don't need to be adjusted anymore
            if (m_TotalPoints < m_ArrowPos.Count)
            {
                m_RopeRenderer.positionCount = m_ArrowPos.Count;
                m_RopeRenderer.SetPosition(0, m_Owner.position);
                for (int i = 0; i < m_ArrowPos.Count; i++)
                {
                    if (i != 0 && i != m_ArrowPos.Count - 1)
                    {
                        Vector3 heading = (m_ArrowPos[i] - m_Owner.position);

                        // remove any points that are further than the full joint distance
                        float distToEnd = heading.magnitude * Vector2.Dot(heading.normalized, m_OwnerToArrowAnchor.normalized);
                        if (distToEnd > m_RopeJoint.distance)
                        {
                            m_ArrowPos.RemoveAt(i);
                            continue;
                        }

                        Vector3 direction = Vector3.Project(heading.normalized, m_OwnerToArrowAnchor);
                        if (direction.magnitude == 0f)
                        {
                            m_TotalPoints += 1;
                        }

                        m_ArrowPos[i] += direction.normalized;
                        m_RopeRenderer.SetPosition(i, m_ArrowPos[i]);
                    }
                }
                m_RopeRenderer.SetPosition(m_ArrowPos.Count - 1, m_CachedTransform.position);
            }
            else
            {
                m_RopeRenderer.positionCount = 2;
                m_RopeRenderer.SetPosition(0, m_Owner.position);
                m_RopeRenderer.SetPosition(1, m_CachedTransform.position);
            }
        }
    }

    public void Launch(Vector2 direction, float power, Transform playerTransform, System.Action callback)
    {
        m_Owner = playerTransform;
        m_Rigidbody.AddForce(direction * power, ForceMode2D.Impulse);
        m_Launched = true;
        m_Trail.enabled = true;

        if (callback != null)
        {
            m_GrappleCallback = callback;
            m_RopeRenderer.enabled = true;

            m_TotalPoints = 0;
            m_ArrowPos.Clear();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        //if (collision.gameObject.layer == LayerMask.NameToLayer("Rope"))
        //{
        //    // cut the rope
        //    if (m_Rigidbody.velocity.magnitude > 5f)
        //    {
        //        HingeJoint2D joint = collision.gameObject.GetComponent<HingeJoint2D>();
        //        if (joint != null)
        //        {
        //            joint.connectedBody = null;
        //            joint.enabled = false;
        //        }
        //    }
        //}
        //else
        //{
            //Debug.Log(m_Velocity.magnitude);
        m_Launched = false;
        m_Trail.enabled = false;

        m_Rigidbody.velocity = Vector3.zero;
        m_Rigidbody.angularVelocity = 0f;
        m_Rigidbody.isKinematic = true;

        // restore the last rotation before the collision
        m_Rigidbody.MoveRotation(m_PrevRotation);

        if (m_GrappleCallback != null)
        {
            SetGrapple(collision.gameObject);
            m_GrappleCallback();
        }
        else
        {
            // TODO change this to when arrows go off screen
            Destroy(this.gameObject, 5f);
        }
        //}
    }

    private void SetGrapple(GameObject hitObj)
    {
        m_RopeJoint.connectedBody = m_Owner.GetComponent<Rigidbody2D>();
        m_OwnerToArrowAnchor = m_CachedTransform.position - m_Owner.position;
        m_RopeJoint.distance = m_OwnerToArrowAnchor.magnitude;
        m_RopeJoint.enabled = true;
    }

    public void Reel(float speed)
    {
        m_RopeJoint.distance -= speed * Time.fixedDeltaTime;
    }

    public void CancelGrapple()
    {
        m_RopeRenderer.enabled = false;
        m_RopeJoint.distance = 0f;
        m_RopeJoint.enabled = false;
        m_GrappleCallback = null;
    }

    public void BreakGrapple()
    {
        m_RopeRenderer.enabled = false;

        m_RopeJoint.connectedBody = null;
        m_RopeJoint.enabled = false;

        Destroy(this.gameObject, 5f);
    }
}
