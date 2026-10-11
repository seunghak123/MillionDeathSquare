using System;
using System.Collections.Generic;
using AppsInToss;
using Cysharp.Threading.Tasks;
using Seunghak.Common;
using Seunghak.LoginSystem;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 앱인토스 인앱결제 구현 (AIT.IAP* 직접 호출, 리플렉션 없음).
    ///
    /// 서버가 없으므로 지급은 클라이언트에서 처리합니다.
    ///   - ProcessProductGrant 콜백 안에서 "동기적으로" Gold를 지급하고 결과를 반환 (블로킹 대기 금지)
    ///   - 지급한 orderId를 PlayerPrefs에 기록해 중복 지급 방지
    ///   - InitAsync에서 결제는 됐지만 지급 완료 통보가 안 된 주문을 복구
    /// </summary>
    public sealed class TossIAP : IPlatformIAP
    {
        /// <summary>지급 완료한 orderId 목록 (',' 구분, 오래된 것부터). 저장 동기화 스냅샷 키 목록에 포함 필요.</summary>
        public const string GrantedOrdersKey = "Toss_IAP_GrantedOrders";

        /// <summary>기록으로 유지할 최근 orderId 개수.</summary>
        private const int MaxGrantedOrders = 50;

        private bool _purchasing;

        public bool IsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        public async UniTask InitAsync()
        {
            if (!IsAvailable()) return;

            try
            {
                var result = await AIT.IAPGetPendingOrders();
                if (result == null || result.Orders == null) return;

                foreach (var order in result.Orders)
                {
                    if (order == null || string.IsNullOrEmpty(order.OrderId)) continue;

                    // 이미 지급했으면 지급은 건너뛰고 완료 통보만 다시 보냄
                    if (!GrantProduct(order.OrderId, order.Sku)) continue;

                    try
                    {
                        await AIT.IAPCompleteProductGrant(new IAPCompleteProductGrantArgs_0
                        {
                            Params = new IAPCompleteProductGrantArgs_0Params { OrderId = order.OrderId }
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[TossIAP] 지급 완료 통보 실패 (orderId={order.OrderId}): {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                // 앱 시작을 막지 않도록 경고만 남김
                Debug.LogWarning($"[TossIAP] 미지급 주문 복구 실패: {e.Message}");
            }
        }

        public async UniTask<ProductInfo[]> GetProductListAsync()
        {
            if (!IsAvailable()) return new ProductInfo[0];

            try
            {
                var result = await AIT.IAPGetProductItemList();
                if (result == null || result.Products == null) return new ProductInfo[0];

                var list = new List<ProductInfo>(result.Products.Length);
                foreach (var p in result.Products)
                {
                    if (p == null) continue;
                    list.Add(new ProductInfo
                    {
                        sku = p.Sku,
                        displayAmount = p.DisplayAmount,
                        displayName = p.DisplayName,
                        iconUrl = p.IconUrl,
                        description = p.Description,
                    });
                }
                return list.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossIAP] 상품 목록 조회 실패: {e.Message}");
                return new ProductInfo[0];
            }
        }

        public async UniTask<PurchaseResult> PurchaseAsync(string sku)
        {
            if (string.IsNullOrEmpty(sku))
                return Fail(sku, "sku is empty");

            if (_purchasing)
                return Fail(sku, "이미 결제가 진행 중입니다.");

#if UNITY_EDITOR
            // 에디터 목 동작: 결제 없이 즉시 지급 후 성공 반환 (UI 테스트용)
            string mockOrderId = "editor-mock-" + Guid.NewGuid().ToString("N");
            if (!GrantProduct(mockOrderId, sku))
                return Fail(sku, "지급 실패 (등록되지 않은 sku)");
            return new PurchaseResult { success = true, orderId = mockOrderId, sku = sku };
#else
            if (!IsAvailable())
                return Fail(sku, "IAP not supported");

            _purchasing = true;
            Action unsubscribe = null;
            try
            {
                var tcs = new UniTaskCompletionSource<PurchaseResult>();
                bool granted = false;
                string grantedOrderId = null;

                var options = new IapCreateOneTimePurchaseOrderOptionsOptions
                {
                    Sku = sku,
                    // SDK(3.5.0)가 { orderId }를 타입 파라미터로 넘겨줌. 동기 처리 후 bool 반환.
                    ProcessProductGrant = data =>
                    {
                        string orderId = data?.OrderId;
                        if (string.IsNullOrEmpty(orderId))
                        {
                            Debug.LogError("[TossIAP] ProcessProductGrant: orderId를 읽을 수 없습니다.");
                            return false;
                        }

                        granted = GrantProduct(orderId, sku);
                        grantedOrderId = orderId;
                        return granted;
                    },
                };

                unsubscribe = AIT.IAPCreateOneTimePurchaseOrder(
                    evt =>
                    {
                        string orderId = evt?.Data?.OrderId ?? grantedOrderId;
                        // 정상적으로는 ProcessProductGrant에서 이미 지급됨. 누락 시 안전망 (중복은 기록으로 차단).
                        if (!granted && !string.IsNullOrEmpty(orderId))
                            granted = GrantProduct(orderId, sku);

                        if (granted)
                            tcs.TrySetResult(new PurchaseResult { success = true, orderId = orderId, sku = sku });
                        else
                            tcs.TrySetResult(Fail(sku, "지급 처리 실패", orderId));
                    },
                    options,
                    err => tcs.TrySetResult(Fail(sku, err != null ? err.Message : "unknown error", grantedOrderId)));

                return await tcs.Task;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TossIAP] 결제 요청 예외: {e.Message}");
                return Fail(sku, e.Message);
            }
            finally
            {
                _purchasing = false;
                try { unsubscribe?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[TossIAP] 구독 해제 실패: {e.Message}"); }
            }
#endif
        }

        private static PurchaseResult Fail(string sku, string error, string orderId = null)
            => new PurchaseResult { success = false, sku = sku, orderId = orderId, error = error };

        /// <summary>
        /// 주문 지급 (동기). 이미 지급한 orderId면 Gold를 다시 주지 않고 true.
        /// TossConfig에 없는 sku는 에러 로그 후 false.
        /// </summary>
        private static bool GrantProduct(string orderId, string sku)
        {
            var granted = LoadGrantedOrders();
            if (granted.Contains(orderId))
            {
                Debug.Log($"[TossIAP] 이미 지급된 주문 (orderId={orderId})");
                return true;
            }

            int gold = TossConfig.Instance.GetGoldAmount(sku);
            if (gold <= 0)
            {
                Debug.LogError($"[TossIAP] TossConfig에 등록되지 않은 sku: {sku} (orderId={orderId})");
                return false;
            }

            UserDataManager.Instance.AddGold(gold);

            // 지급 직후 기록 (앱이 꺼져도 중복 지급되지 않도록 즉시 저장)
            granted.Add(orderId);
            if (granted.Count > MaxGrantedOrders)
                granted.RemoveRange(0, granted.Count - MaxGrantedOrders);
            PlayerPrefs.SetString(GrantedOrdersKey, string.Join(",", granted));
            PlayerPrefs.Save();
            PlatformSaveSync.MarkDirty();
            PlatformSaveSync.FlushNow(); // 결제 지급분은 디바운스 없이 바로 원격에 반영

            Debug.Log($"[TossIAP] 지급 완료 sku={sku} gold={gold} orderId={orderId}");
            return true;
        }

        private static List<string> LoadGrantedOrders()
        {
            string raw = PlayerPrefs.GetString(GrantedOrdersKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return new List<string>(raw.Split(','));
        }
    }
}
