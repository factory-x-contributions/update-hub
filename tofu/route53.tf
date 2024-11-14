resource "aws_route53_zone" "irs_zone" {
  name = "factory-x.${var.domain_name}"
}

resource "aws_route53_record" "irs_record" {
  zone_id = aws_route53_zone.irs_zone.zone_id
  name    = "irs.factory-x.${var.domain_name}"
  type    = "A"

  alias {
    name                   = aws_alb.irs.dns_name
    zone_id                = aws_alb.irs.zone_id
    evaluate_target_health = true
  }
}