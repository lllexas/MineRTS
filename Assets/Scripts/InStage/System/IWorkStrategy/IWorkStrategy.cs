public interface IWorkStrategy
{
    // 传入满意度
    void Tick(int index, WholeComponent whole, float deltaTime);
}