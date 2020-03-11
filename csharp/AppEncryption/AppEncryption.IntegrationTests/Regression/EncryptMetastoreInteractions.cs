using GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    public class EncryptMetastoreInteractions
    {
        private readonly KeyState cacheIK;
        private readonly KeyState metaIK;
        private readonly KeyState cacheSK;
        private readonly KeyState metaSK;

        protected internal EncryptMetastoreInteractions(
            KeyState cacheIK, KeyState metaIK, KeyState cacheSK, KeyState metaSK)
        {
            this.cacheIK = cacheIK;
            this.metaIK = metaIK;
            this.cacheSK = cacheSK;
            this.metaSK = metaSK;
        }

        protected internal bool ShouldStoreIK()
        {
            if (cacheIK == KeyState.Valid)
            {
                return false;
            }

            if (metaIK != KeyState.Valid)
            {
                return true;
            }

            // At this stage IK is valid in metastore
            // The existing IK can only be used if the SK is valid in cache
            // or if the SK is missing from the cache but is valid in metastore
            if (cacheSK == KeyState.Valid)
            {
                return false;
            }
            else if (cacheSK == KeyState.Empty)
            {
                if (metaSK == KeyState.Valid)
                {
                    return false;
                }
            }

            return true;
        }

        protected internal bool ShouldStoreSK()
        {
            if (cacheIK == KeyState.Valid)
            {
                return false;
            }

            return cacheSK != KeyState.Valid && metaSK != KeyState.Valid;
        }

        protected internal bool ShouldLoadSK()
        {
            if (cacheIK == KeyState.Valid)
            {
                return false;
            }

            if (metaIK != KeyState.Valid)
            {
                return false;
            }

            if (cacheSK == KeyState.Empty)
            {
                return true;
            }

            return false;
        }

        protected internal bool ShouldLoadLatestIK()
        {
            return cacheIK != KeyState.Valid;
        }

        protected internal bool ShouldLoadLatestSK()
        {
            if (cacheIK == KeyState.Valid)
            {
                return false;
            }

            if (metaIK == KeyState.Valid)
            {
                // Since the cache SK is retired, we create a new IK.
                // Because the cache SK happens to be the latest one in cache,
                // we need to load the latest SK from metastore during IK creation flow
                if (cacheSK == KeyState.Retired)
                {
                    return true;
                }

                // Since the SK is not in the cache and not valid in the metastore, we have to create a new IK.
                // This requires loading the latest SK from metastore during IK creation flow
                return cacheSK == KeyState.Empty && metaSK != KeyState.Valid;
            }

            return cacheSK != KeyState.Valid;
        }
    }
}
