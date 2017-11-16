namespace Nevermind.Core
{
    public class TangerineWhistleProtocolSpecification : IProtocolSpecification
    {
        public bool IsEip2Enabled => true;
        public bool IsEip7Enabled => true;
        public bool IsEip8Enabled => true;
        public bool IsEip150Enabled => true;
        public bool IsEip155Enabled => false;
        public bool IsEip158Enabled => false; // also called EIP-161
        public bool IsEip160Enabled => false;
        public bool IsEip170Enabled => false;
        public bool IsEmptyCodeContractBugFixed => true; // ???
        public bool IsEip186Enabled => false;
    }
}