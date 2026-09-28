using UnityEngine;

// Arranca la animación de cada persona en un punto distinto de su ciclo.
// Sin esto, todos los vecinos que usan la misma animación de "quieto" se
// rascarían la cabeza exactamente al mismo tiempo, y se nota muchísimo.
public class AnimatorRandomStart : MonoBehaviour
{
    private void Start()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>())
        {
            if (animator.runtimeAnimatorController == null) continue;
            animator.Play(0, 0, Random.value);
            animator.speed = Random.Range(0.9f, 1.1f);
        }
    }
}
