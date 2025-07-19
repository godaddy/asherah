using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class DecryptMetastoreInteractions
    {
        private readonly KeyState cacheIK;
        private readonly KeyState cacheSK;

        public DecryptMetastoreInteractions(KeyState cacheIK, KeyState cacheSK)
        {
            this.cacheIK = cacheIK;
            this.cacheSK = cacheSK;
        }

        protected internal bool ShouldLoadIK()
        {
            return cacheIK == KeyState.Empty;
        }

        protected internal bool ShouldLoadSK()
        {
            if (ShouldLoadIK())
            {
                return cacheSK == KeyState.Empty;
            }

            return false;
        }
    }
}
