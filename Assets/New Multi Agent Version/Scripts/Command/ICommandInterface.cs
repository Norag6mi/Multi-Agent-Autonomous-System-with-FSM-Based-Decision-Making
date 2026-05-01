using UnityEngine;

public interface ICommandInterface
{
    void IssueMoveOrder(Vector3 worldPosition);
    void IssueRegroup(Vector3 regroupPosition);
    void IssueHold();
    void IssueRelease();
    void SelectAgent(AgentIdentity agent);
    void SelectAllAllies();
}