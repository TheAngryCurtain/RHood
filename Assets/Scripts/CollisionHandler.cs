using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum eCollisionZone { Below, Above, Left, Right }

public class CollisionHandler : MonoBehaviour
{
    [SerializeField] private Controller m_Controller;
    [SerializeField] private eCollisionZone m_Zone;
    [SerializeField] private LayerMask m_CollisionMask;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (LayerInLayerMask(collision.gameObject.layer, m_CollisionMask))
        {
            m_Controller.HandleTriggerCollision(m_Zone, true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (LayerInLayerMask(collision.gameObject.layer, m_CollisionMask))
        {
            m_Controller.HandleTriggerCollision(m_Zone, false);
        }
    }

    public static bool LayerInLayerMask(int layer, LayerMask mask)
    {
        return mask == (mask | (1 << layer));
    }
}
