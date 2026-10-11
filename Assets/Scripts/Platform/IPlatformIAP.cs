using Cysharp.Threading.Tasks;
using Seunghak.LoginSystem;

namespace Seunghak.Platform
{
    /// <summary>
    /// 플랫폼별 인앱결제 추상화.
    ///
    /// 구현체:
    ///   - TossIAP — 앱인토스 인앱결제 (AIT.IAP*)
    ///   - NullIAP — 미지원 플랫폼용 no-op 구현
    /// </summary>
    public interface IPlatformIAP
    {
        /// <summary>현재 환경에서 인앱결제가 가능한지 확인합니다.</summary>
        bool IsAvailable();

        /// <summary>초기화. 지급이 끝나지 않은 주문이 있으면 여기서 복구 지급합니다.</summary>
        UniTask InitAsync();

        /// <summary>구매 가능한 상품 목록. 실패 시 빈 배열.</summary>
        UniTask<ProductInfo[]> GetProductListAsync();

        /// <summary>상품 결제 + 지급. 지급까지 끝난 뒤 결과를 반환합니다.</summary>
        UniTask<PurchaseResult> PurchaseAsync(string sku);
    }

    public sealed class PurchaseResult
    {
        public bool success;
        public string orderId;
        public string sku;
        /// <summary>실패 시 에러 메시지 (사용자 취소 포함).</summary>
        public string error;
    }

    public sealed class NullIAP : IPlatformIAP
    {
        public static readonly NullIAP Instance = new NullIAP();
        public bool IsAvailable() => false;
        public UniTask InitAsync() => UniTask.CompletedTask;
        public UniTask<ProductInfo[]> GetProductListAsync() => UniTask.FromResult(new ProductInfo[0]);
        public UniTask<PurchaseResult> PurchaseAsync(string sku)
            => UniTask.FromResult(new PurchaseResult { success = false, sku = sku, error = "IAP not supported" });
    }
}
