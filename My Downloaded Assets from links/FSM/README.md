# Example Usage

    stateMachine = new StateMachine();

    // STATES
    var idleState   = new ZombieIdleState(zombieStats, navMesh, animator);
    var wanderState = new ZombieWanderState(zombieStats, navMesh, animator);
    var hordeState  = new ZombieHordeState(this, zombieStats, navMesh, animator);
    var aggroState  = new ZombieAggroState(this, zombieStats, navMesh, animator);
    var attackState = new ZombieAttackState(this, zombieStats, attackController, animator);
    var hitState    = new ZombieHitState(this, animator);
    var deathState  = new ZombieDeathState(this, ragdollController, animator, hitCollider);


    // TRANSITIONS

    At(idleState, wanderState, GoWander());
    At(wanderState, idleState, GoIdle());
    At(idleState, aggroState, HasAttentionTarget());
    At(wanderState, aggroState, HasAttentionTarget());
    At(hordeState, aggroState, HasAttentionTarget()); // What if this is a zombie?

    At(idleState, hordeState, GoHorde());
    At(wanderState, hordeState, GoHorde());

    Any(attackState, CanAttack());
    At(attackState, idleState, IsDoneAttacking());

    Any(hitState, CheckConfusedState(true));
    At(hitState, idleState, CheckConfusedState(false));

    Any(deathState, IsDead());

    // START STATE
    stateMachine.SetState(idleState);

    // FUNTIONS & CONDTIONS
    void At(IState from, IState to, Func<bool> condition) => stateMachine.AddTransition(from, to, condition);
    void Any(IState to, Func<bool> condition) => stateMachine.AddAnyTransition(to, condition);
    Func<bool> GoWander() => () => idleState.ShouldStopIdle();
    Func<bool> GoIdle() => () => wanderState.IsDone();
    Func<bool> GoHorde() => () => hordeController != null;

    Func<bool> HasAttentionTarget() => () => attentionTarget != null;
    Func<bool> CheckConfusedState(bool check) => () => (IsConfused == check) && !healthController.IsDead();
    Func<bool> IsDead() => () => healthController.IsDead();

    Func<bool> CanAttack() => () => attackController.CanLaunchAttack();
    Func<bool> IsDoneAttacking() => () => attackController.attack == false;
