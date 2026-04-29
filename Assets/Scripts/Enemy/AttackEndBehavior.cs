using UnityEngine;

public class AttackEndBehaviour : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Use GetComponentInParent in case the Animator is located on a child model GameObject
        BaseEnemy enemy = animator.GetComponentInParent<BaseEnemy>();
        if (enemy == null) return;

        // Only call OnAttackEnd if the enemy is actually in an attack
        if (enemy.IsAttacking())
        {
            enemy.OnAttackEnd();
        }
    }
}