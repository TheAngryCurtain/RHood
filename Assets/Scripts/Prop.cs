using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Prop : MonoBehaviour
{
    [SerializeField] protected Transform m_CachedTransform;
    [SerializeField] protected Rigidbody2D m_Rigidbody;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject obj = collision.gameObject;
        if (obj.layer == LayerMask.NameToLayer("Arrow"))
        {
            obj.transform.SetParent(m_CachedTransform);
        }
    }
}
